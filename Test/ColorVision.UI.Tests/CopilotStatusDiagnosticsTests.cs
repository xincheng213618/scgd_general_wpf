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
            ActiveProviderRetryRequestId = "req_status_429",
        });

        Assert.Contains(
            "供应商保护：首个可显示内容 90 秒 / 流更新停滞 45 秒 / 最多 4 次请求",
            report,
            StringComparison.Ordinal);
        Assert.Contains(
            "当前运行重试：2 次 · 最近 3/4 · HTTP 429 · 请求 req_status_429 · 计划等待 1.5 秒",
            report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StatusIncludesCopyableConversationIdentityAndRecoveryState()
    {
        var report = CopilotStatusDiagnostics.Format(new CopilotStatusDiagnosticSnapshot
        {
            HasConversation = true,
            ConversationTitle = "Camera recovery",
            ConversationId = "conversation-123",
            ConversationVisibleTurns = 4,
            ConversationMessageCount = 9,
            ConversationQueuedFollowUps = 2,
            ConversationHasCheckpoint = true,
            ConversationHasRecoverableAgentTasks = true,
            ConversationIsBranch = true,
            ConversationParentId = "conversation-parent",
            ConversationRootId = "conversation-root",
            AdditionalReadRootCount = 2,
        });

        Assert.Contains("会话：Camera recovery", report, StringComparison.Ordinal);
        Assert.Contains("会话 ID：conversation-123", report, StringComparison.Ordinal);
        Assert.Contains("可见历史：4 轮请求 · 9 条消息", report, StringComparison.Ordinal);
        Assert.Contains(
            "恢复：有可安全继续的 Agent 任务 · 2 条排队后续",
            report,
            StringComparison.Ordinal);
        Assert.Contains(
            "分支：父会话 conversation-parent · 根会话 conversation-root",
            report,
            StringComparison.Ordinal);
        Assert.Contains("附加只读目录：2", report, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusReportsLiveConversationRunAheadOfSavedCheckpoint()
    {
        var report = CopilotStatusDiagnostics.Format(new CopilotStatusDiagnosticSnapshot
        {
            HasConversation = true,
            ConversationRunState = CopilotHostedRunState.Running,
            ConversationHasCheckpoint = true,
            ConversationHasRecoverableAgentTasks = true,
        });

        Assert.Contains("恢复：Agent 运行中", report, StringComparison.Ordinal);
        Assert.DoesNotContain("有可安全继续的 Agent 任务", report, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionInfoAliasUsesStatusCommandWithoutArguments()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/session-info");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Status, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Null(CopilotLocalCommandCatalog.Parse("/session-info now"));
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
            429,
            "req_latest_retry");

        run.RecordProviderRetry(first);
        run.RecordProviderRetry(latest);

        Assert.Equal(2, run.ProviderRetrySnapshot.Count);
        Assert.Same(latest, run.ProviderRetrySnapshot.Latest);
        Assert.Equal(
            "req_latest_retry",
            run.ProviderRetrySnapshot.Latest?.RequestId);
        run.Complete(null);
    }
}
