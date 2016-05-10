using System;

namespace Toggl.Phoebe
{
    public static class Build
    {
        #region Phoebe build config

#if DEBUG
        public static readonly Uri ApiUrl = new Uri("https://mobile.toggl.space/api/");
        public static readonly Uri ReportsApiUrl = new Uri("https://mobile.toggl.space/reports/api/");
#else
        public static readonly Uri ApiUrl = new Uri("https://mobile.toggl.com/api/");
        public static readonly Uri ReportsApiUrl = new Uri("https://mobile.toggl.com/reports/api/");
#endif
        public static readonly Uri StagingUrl = new Uri("https://mobile.toggl.space/api/");
        public static readonly Uri PrivacyPolicyUrl = new Uri("https://toggl.com/legal/privacy");
        public static readonly Uri TermsOfServiceUrl = new Uri("https://toggl.com/legal/terms");
        public static readonly string GoogleAnalyticsId = "{GOOGLE_ANALYTICS_ID}";
        public static readonly int GoogleAnalyticsPlanIndex = 1;
        public static readonly int GoogleAnalyticsExperimentIndex = 2;

        #endregion

        #region Joey build configuration

#if __ANDROID__
        public static readonly string AppIdentifier = "TogglJoey";
        public static readonly string GcmSenderId = "{GMC_SENDER_ID}";
        public static readonly string XamarinInsightsApiKey = "{XAMARIN_INSIGHTS_API_KEY_ANDROID}";
        public static readonly string GooglePlayUrl = "https://play.google.com/store/apps/details?id=com.toggl.timer";
        public const Net.PushService PushService = Net.PushService.APNS;
#endif
        #endregion

        #region Ross build configuration

#if __IOS__
        public static readonly string AppIdentifier = "TogglRoss";
        public static readonly string AppStoreUrl = "itms-apps://itunes.com/apps/toggl";
        public static readonly string XamarinInsightsApiKey = "{XAMARIN_INSIGHTS_API_KEY_IOS}";
        public static readonly string TestFairyApiToken = "{TESTFAIRY_API_TOKEN}";
        public static readonly string GoogleClientId = "{GOOGLE_CLIENT_ID}";
        public const Net.PushService PushService = Net.PushService.APNS;
#endif
        #endregion
    }
}
