using System;
using System.Diagnostics;
using Android.App;
using Android.Content;
using SQLite.Net.Interop;
using SQLite.Net.Platform.XamarinAndroid;
using Xamarin;
using Toggl.Joey.Analytics;
using Toggl.Joey.Logging;
using Toggl.Joey.Net;
using Toggl.Joey.UI.Activities;
using Toggl.Phoebe;
using Toggl.Phoebe.Net;
using Toggl.Phoebe.Reactive;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Logging;
using XPlatUtils;
using Toggl.Phoebe.Misc;
using Toggl.Joey.Data;

namespace Toggl.Joey
{
    [Application(
         Icon = "@drawable/Icon",
         Label = "@string/AppName",
         Description = "@string/AppDescription",
         Theme = "@style/Theme.Toggl.App")]
    [MetaData("com.google.android.gms.version",
              Value = "@integer/google_play_services_version")]
    class AndroidApp : Application, IPlatformUtils
    {
        private bool componentsInitialized;
        private Stopwatch startTimeMeasure = Stopwatch.StartNew();

        public AndroidApp()
        {
        }

        public AndroidApp(IntPtr javaRef, Android.Runtime.JniHandleOwnership transfer) : base(javaRef, transfer)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            RegisterComponents();
            InitializeStartupComponents();
        }

        private void RegisterComponents()
        {
            // Attach bug tracker
#if DEBUG
            Insights.Initialize(Insights.DebugModeKey, Context);
#else
            Insights.Initialize(Build.XamarinInsightsApiKey, Context);
#endif

            // Register platform service.
            ServiceContainer.Register<IPlatformUtils> (this);
            ServiceContainer.Register<ITimeProvider> (() => new DefaultTimeProvider());
            ServiceContainer.Register<INetworkPresence> (() => new NetworkPresence());

            // Used for migration
            ServiceContainer.Register<IOldSettingsStore>(() => new OldSettingsStore(Context));

            // Register Phoebe services.
            Services.Register();

            // Register Joey components:
            ServiceContainer.Register<ILogger> (() => new Logger());
            ServiceContainer.Register<Context> (this);
            ServiceContainer.Register<AndroidNotificationManager> ();
            ServiceContainer.Register<ILoggerClient> (() => new LogClient());
            var tracker = new Tracker(this);
            ServiceContainer.Register<ITracker> (() => tracker);

            // This needs some services, like ITimeProvider, so run it at the end
            RxChain.Init(AppState.Init());
        }

        private void InitializeStartupComponents()
        {
            //ServiceContainer.Resolve<UpgradeManger> ().TryUpgrade ();
            ServiceContainer.Resolve<ILoggerClient> ();
        }

        public void InitializeComponents()
        {
            if (componentsInitialized)
            {
                return;
            }

            componentsInitialized = true;
            // TODO RX: Move this somewhere else. Previously it was ServiceContainer.Resolve<GcmRegistrationManager> ();
            RxChain.Send(new Phoebe.Data.DataMsg.RegisterPush());
            ServiceContainer.Resolve<AndroidNotificationManager> ();
        }

        public void MarkLaunched()
        {
            if (!startTimeMeasure.IsRunning)
            {
                return;
            }

            startTimeMeasure.Stop();
            ServiceContainer.Resolve<ITracker> ().SendAppInitTime(startTimeMeasure.Elapsed);
        }

        public override void OnTrimMemory(TrimMemory level)
        {
            base.OnTrimMemory(level);

            if (level <= TrimMemory.Moderate)
            {
                if (level <= TrimMemory.Complete)
                {
                    GC.Collect(GC.MaxGeneration);
                }
                else
                {
                    GC.Collect();
                }
            }
        }

        public string AppIdentifier
        {
            get { return Build.AppIdentifier; }
        }

        public string AppVersion
        {
            get { return PackageManager.GetPackageInfo(PackageName, 0).VersionName; }
        }

        // Property to match with the IPlatformUtils
        // interface. This interface is implemented by iOS and Android.
        public bool IsWidgetAvailable
        {
            get { return true; }
        }

        public ISQLitePlatform SQLiteInfo
        {
            get
            {
                return new SQLitePlatformAndroid();
            }
        }

        public bool ComponentsInitialized
        {
            get { return componentsInitialized; }
        }

        public void DispatchOnUIThread(Action action)
        {
            BaseActivity.CurrentActivity.RunOnUiThread(action);
        }
    }
}
