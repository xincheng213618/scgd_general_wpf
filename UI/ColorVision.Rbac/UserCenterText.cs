using ColorVision.Solution.Properties;
using System.Globalization;

namespace ColorVision.Rbac
{
    public static class UserCenterText
    {
        public static string Profile => Get(nameof(Profile));
        public static string Description => Get(nameof(Description));
        public static string CurrentSession => Get(nameof(CurrentSession));
        public static string TotalRuntime => Get(nameof(TotalRuntime));
        public static string LaunchCount => Get(nameof(LaunchCount));
        public static string TotalFlowCount => Get(nameof(TotalFlowCount));
        public static string Last7CompletionRate => Get(nameof(Last7CompletionRate));
        public static string FlowActivity => Get(nameof(FlowActivity));
        public static string RefreshStatistics => Get(nameof(RefreshStatistics));
        public static string AccountOverview => Get(nameof(AccountOverview));
        public static string AccountUpdated => Get(nameof(AccountUpdated));
        public static string FirstUsage => Get(nameof(FirstUsage));
        public static string FlowInsights => Get(nameof(FlowInsights));
        public static string Last7Executions => Get(nameof(Last7Executions));
        public static string AverageFlowDuration => Get(nameof(AverageFlowDuration));
        public static string ActiveDays => Get(nameof(ActiveDays));
        public static string BusiestDay => Get(nameof(BusiestDay));
        public static string LastLaunch => Get(nameof(LastLaunch));
        public static string ActivityLess => Get(nameof(ActivityLess));
        public static string ActivityMore => Get(nameof(ActivityMore));

        private static string Get(string name) =>
            Resources.ResourceManager.GetString("Sol_UserCenter_" + name, CultureInfo.CurrentUICulture) ?? name;
    }
}
