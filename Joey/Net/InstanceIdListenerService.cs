﻿using Android.App;
using Android.Content;
using Android.Gms.Gcm.Iid;
using Toggl.Joey.Net;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Reactive;
using XPlatUtils;

namespace ClientApp
{
    // Registration tokens are unique and secure; however, the client app(or GCM) may need
    // to refresh the registration token in the event of app reinstallation or a security issue.
    // https://developer.xamarin.com/guides/cross-platform/application_fundamentals/notifications/android/remote_notifications_in_android/#Implement_an_Instance_ID_Listener_Service
    [Service(Exported = false), IntentFilter(new[] { "com.google.android.gms.iid.InstanceID" })]
    class InstanceIDListenerService : Android.Gms.Gcm.Iid.InstanceIDListenerService
    {
        public override void OnTokenRefresh()
        {
            RxChain.Send(new DataMsg.RegisterPush());
        }
    }
}
