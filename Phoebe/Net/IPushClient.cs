using System;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Net
{
    public interface IPushClient
    {
        string GetPushToken();

        Task Register(string authToken, PushService service, string pushToken);

        Task Unregister(string authToken, PushService service, string pushToken);
    }
}
