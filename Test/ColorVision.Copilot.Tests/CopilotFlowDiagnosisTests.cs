using ColorVision.Copilot.Mcp;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowDiagnosisTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Timeout=30000")]
    public async Task StateAndConfigurationRemainCluesWithoutBecomingFailureCauses(string hostExcerpt)
    {
        var snapshot = new CopilotFlowContextSnapshot
        {
            FlowName = "Camera inspection",
            Status = "Running",
            IsRunning = true,
            FailureEvidence = string.IsNullOrEmpty(hostExcerpt) ? [] : [hostExcerpt],
            Nodes =
            [
                new CopilotFlowNodeContextSnapshot
                {
                    NodeId = "camera-1",
                    Title = "Camera",
                    IsSelected = true,
                    Parameters = [new CopilotContextProperty { Name = "Timeout", Value = "30000" }],
                },
            ],
        };
        var liveContext = new CopilotLiveContext
        {
            SourceId = "template-json-editor:test",
            SnapshotItems =
            [
                new CopilotContextItem
                {
                    Content = "```json\n{\"Timeout\":30000,\"Gain\":1,\"Min\":0,\"Max\":255}\n```",
                },
            ],
        };

        var result = await DiagnoseAsync(snapshot, new CopilotCapabilityResult
        {
            Success = true,
            Summary = "Recent log search; keyword=error",
            Content = "INFO Running; Timeout=30000",
        }, liveContext);

        Assert.True(result.Success, result.Text);
        Assert.Contains("- Status: Running", result.Text, StringComparison.Ordinal);
        Assert.Contains("- Parameters: Timeout=30000", result.Text, StringComparison.Ordinal);
        Assert.Contains("Top-level keys: Timeout, Gain, Min, Max", result.Text, StringComparison.Ordinal);
        Assert.Contains("not verified failures or root causes", result.Text, StringComparison.Ordinal);
        Assert.Contains("No root cause has been established", result.Text, StringComparison.Ordinal);
        Assert.Contains("Requested maximum log lines: 120", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Inspected log lines", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Likely Causes", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("evidence is present", result.Text, StringComparison.Ordinal);
        if (hostExcerpt.Length == 0)
            Assert.Contains("No failure excerpt was provided; this does not establish that the flow succeeded.", result.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualFailureTextIsPreservedWithItsSourceAndCorrelationBoundary(bool useLegacySummary)
    {
        const string failure = "10:01:02 ERROR run=run-7 node=camera-1 acquisition timed out";
        var result = await DiagnoseAsync(new CopilotFlowContextSnapshot
        {
            Status = "Failed",
            FailureEvidence = useLegacySummary ? [] : [failure],
            RecentFailureSummary = useLegacySummary ? failure : string.Empty,
        }, new CopilotCapabilityResult
        {
            Success = true,
            Content = "09:50:00 ERROR run=run-6 node=network-2 socket disconnected",
        });

        Assert.True(result.Success, result.Text);
        Assert.Contains(failure, result.Text, StringComparison.Ordinal);
        Assert.Contains("Source: active flow snapshot", result.Text, StringComparison.Ordinal);
        Assert.Contains("run=run-6 node=network-2 socket disconnected", result.Text, StringComparison.Ordinal);
        Assert.Contains("not correlated to the current flow run, batch or node", result.Text, StringComparison.Ordinal);
        Assert.Contains("Match the flow/batch, node and timestamps", result.Text, StringComparison.Ordinal);
        Assert.Contains("No root cause has been established", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("evidence is present", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSnapshotAndLogReadFailureDoNotBecomeAFlowFailureDiagnosis()
    {
        var result = await DiagnoseAsync(null, new CopilotCapabilityResult
        {
            Success = false,
            ErrorMessage = "Timeout while reading the application log",
            Content = "Unusable partial log content",
        });

        Assert.True(result.Success, result.Text);
        Assert.Contains("No active flow snapshot is available", result.Text, StringComparison.Ordinal);
        Assert.Contains("Recent log unavailable: Timeout while reading the application log", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Unusable partial log content", result.Text, StringComparison.Ordinal);
        Assert.Contains("No root cause has been established", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Timeout evidence is present", result.Text, StringComparison.Ordinal);
    }

    private static Task<CopilotMcpToolCallResult> DiagnoseAsync(
        CopilotFlowContextSnapshot? snapshot,
        CopilotCapabilityResult log,
        CopilotLiveContext? liveContext = null)
    {
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            FlowSnapshotProvider = _ => Task.FromResult(snapshot),
            RecentLogProvider = (_, _, _, _, _) => Task.FromResult(log),
            LiveContextProvider = () => liveContext,
        });
        return dispatcher.CallAsync("diagnose_flow_failure", null, CancellationToken.None);
    }
}
