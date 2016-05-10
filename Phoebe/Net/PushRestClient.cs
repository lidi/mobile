using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using XPlatUtils;

namespace Toggl.Phoebe.Net
{
    public enum PushService
    {
        GCM,
        APNS
    }

    public class PushRestClient : IPushClient
    {
        private readonly Uri v8Url;

        public PushRestClient(Uri url)
        {
            v8Url = new Uri(url, "v8/push_service/");
        }

        private HttpClient MakeHttpClient()
        {
            // Cannot share HttpClient instance between threads as it might (and will) cause InvalidOperationExceptions
            // occasionally.
#if __TESTS__
            var client = new HttpClient()
#else
            var client = new HttpClient(new ModernHttpClient.NativeMessageHandler())
#endif
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
            var headers = client.DefaultRequestHeaders;
            headers.UserAgent.Clear();
            headers.UserAgent.Add(new ProductInfoHeaderValue(Platform.AppIdentifier, Platform.AppVersion));
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }


        private string GetPayload(PushService service, string regid)
        {
            switch (service)
            {
                case PushService.GCM:
                    return new JObject(
                               new JProperty("pushservicetype", "gcm"),
                               new JProperty("regid", regid)
                           ).ToString();
                case PushService.APNS:
                    return new JObject(
                               new JProperty("pushservicetype", "apns"),
                               new JProperty("devtoken", regid)
                           ).ToString();
                default:
                    throw new ArgumentException("Unknown service", "service");
            }
        }

        private void AddAuthToken(string authToken, HttpRequestMessage req)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes(
                                               string.Format("{0}:api_token", authToken))));
        }

        public string GetPushToken()
        {
            var token = string.Empty;

            // TODO: Probably not the best way to do this, we can move this code to the platform projects later
#if __ANDROID__
            var ctx = ServiceContainer.Resolve<Android.Content.Context>();

            // Check Google Play Services availability
            if (Android.Gms.Common.GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(ctx)
                    == Android.Gms.Common.ConnectionResult.Success)
            {
                var instanceID = Android.Gms.Gcm.Iid.InstanceID.GetInstance(ctx);
                token = instanceID.GetToken(Build.GcmSenderId, Android.Gms.Gcm.GoogleCloudMessaging.InstanceIdScope, null);
            }
#elif __IOS__
            // TODO
#endif

            return token;
        }

        public async Task Register(string authToken, PushService service, string pushToken)
        {
            using(var httpClient = MakeHttpClient())
            {
                var url = new Uri(v8Url, "subscribe");
                string json = GetPayload(service, pushToken);

                var httpReq = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = url,
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                AddAuthToken(authToken, httpReq);

                await httpClient.SendAsync(httpReq).ConfigureAwait(false);
            }
        }

        public async Task Unregister(string authToken, PushService service, string pushToken)
        {
            using(var httpClient = MakeHttpClient())
            {
                var url = new Uri(v8Url, "unsubscribe");
                string json = GetPayload(service, pushToken);

                var httpReq = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = url,
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                AddAuthToken(authToken, httpReq);

                await httpClient.SendAsync(httpReq).ConfigureAwait(false);
            }
        }
    }
}
