using Android.App;
using Android.Content;
using Android.Support.V4.Content;

namespace Toggl.Joey.Net
{
    [BroadcastReceiver(Permission = "com.google.android.c2dm.permission.SEND")]
    [IntentFilter(new [] { "com.google.android.c2dm.intent.RECEIVE" },
                  Categories = new [] { "com.toggl.timer" })]
    public class GcmBroadcastReceiver : WakefulBroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            Phoebe.Reactive.RxChain.Send(new Phoebe.Data.ServerRequest.GetChanges());
            ResultCode = Result.Ok;
        }
    }
}
