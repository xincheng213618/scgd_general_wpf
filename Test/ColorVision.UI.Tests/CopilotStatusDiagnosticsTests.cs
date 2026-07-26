using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotStatusDiagnosticsTests
{
    [Fact]
    public void ApplicationVersionPreservesFourPartRevision()
    {
        var version = new Version(1, 4, 11, 1);

        var formatted = CopilotStatusDiagnostics.FormatApplicationVersion(version);

        Assert.Equal("1.4.11.1", formatted);
    }

    [Fact]
    public void ApplicationVersionFallsBackWhenAssemblyVersionIsUnavailable()
    {
        Assert.Equal("unknown", CopilotStatusDiagnostics.FormatApplicationVersion(null));
    }

    [Fact]
    public void StatusIncludesProviderPolicyAndActiveRetryDetails()
    {
        var report = CopilotStatusDiagnostics.Format(new CopilotStatusDiagnosticSnapshot
        {
            ProviderFirstContentTimeoutSeconds = 90,
            ProviderStreamingInactivityTimeoutSeconds = 45,
            ProviderMaximumAttempts = 4,
            ActiveProviderRetryCount = 2,
            ActiveProviderRetryNextAttempt = 3,
            ActiveProviderRetryMaximumAttempts = 4,
            ActiveProviderRetryDelayMilliseconds = 1_500,
            ActiveProviderRetryFailureKind = "HTTP 429",
        });

        Assert.Contains(
            "供应商保护：首个可显示内容 90 秒 / 流更新停滞 45 秒 / 最多 4 次请求",
            report,
            StringComparison.Ordinal);
        Assert.Contains(
            "当前运行重试：2 次 · 最近 3/4 · HTTP 429 · 计划等待 1.5 秒",
            report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HostedRunRetainsRetryCountAndLatestDetails()
    {
        using var run = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        var first = new CopilotProviderRetryInfo(
            1,
            2,
            3,
            TimeSpan.FromMilliseconds(250),
            "connection failure",
            null);
        var latest = new CopilotProviderRetryInfo(
            2,
            3,
            3,
            TimeSpan.FromSeconds(1),
            "HTTP 429",
            429);

        run.RecordProviderRetry(first);
        run.RecordProviderRetry(latest);

        Assert.Equal(2, run.ProviderRetrySnapshot.Count);
        Assert.Same(latest, run.ProviderRetrySnapshot.Latest);
        run.Complete(null);
    }
}
