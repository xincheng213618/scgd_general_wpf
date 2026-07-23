using System.Globalization;
using SolutionResources = ColorVision.Solution.Properties.Resources;

namespace ColorVision.UI.Tests;

public sealed class UserCenterLocalizationTests
{
    private static readonly string[] ResourceKeys =
    {
        "Sol_UserCenter_Profile",
        "Sol_UserCenter_Description",
        "Sol_UserCenter_CurrentSession",
        "Sol_UserCenter_TotalRuntime",
        "Sol_UserCenter_LaunchCount",
        "Sol_UserCenter_TotalFlowCount",
        "Sol_UserCenter_Last7CompletionRate",
        "Sol_UserCenter_FlowActivity",
        "Sol_UserCenter_RefreshStatistics",
        "Sol_UserCenter_AccountOverview",
        "Sol_UserCenter_AccountUpdated",
        "Sol_UserCenter_FirstUsage",
        "Sol_UserCenter_FlowInsights",
        "Sol_UserCenter_Last7Executions",
        "Sol_UserCenter_AverageFlowDuration",
        "Sol_UserCenter_ActiveDays",
        "Sol_UserCenter_BusiestDay",
        "Sol_UserCenter_LastLaunch",
        "Sol_UserCenter_ActivityLess",
        "Sol_UserCenter_ActivityMore",
    };

    public static TheoryData<string, string> SupportedCultures => new()
    {
        { "zh-CN", "" },
        { "zh-Hant", "zh-Hant" },
        { "en", "en" },
        { "fr", "fr" },
        { "ja", "ja" },
        { "ko", "ko" },
        { "ru", "ru" },
    };

    [Theory]
    [MemberData(nameof(SupportedCultures))]
    public void EverySupportedCultureContainsUserCenterResources(string cultureName, string resourceCultureName)
    {
        var culture = string.IsNullOrEmpty(resourceCultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(resourceCultureName);
        var resourceSet = SolutionResources.ResourceManager.GetResourceSet(culture, true, false);

        Assert.NotNull(resourceSet);
        foreach (var key in ResourceKeys)
            Assert.False(string.IsNullOrWhiteSpace(resourceSet.GetString(key)), $"Missing {key} for {cultureName}");
    }
}
