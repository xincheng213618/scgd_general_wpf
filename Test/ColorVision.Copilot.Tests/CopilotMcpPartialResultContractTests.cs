using ColorVision.Copilot.Mcp;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMcpPartialResultContractTests
{
    [Theory]
    [InlineData(false, "")]
    [InlineData(false, "Bounded evidence: password=synthetic-private-value")]
    [InlineData(true, "")]
    [InlineData(true, "Bounded evidence: password=synthetic-private-value")]
    public async Task ExplicitCoverageSurvivesMcpAndApplicationSnapshotsWithoutChangingTheBody(bool contentOnly, string partial)
    {
        const string body = "{\"has_more\":true,\"partial\":\"body text is not status\",\"runs\":[]}";
        const string summary = "Legacy summary";
        var dispatcher = Dispatcher(new CopilotCapabilityResult { Success = true, Summary = summary, Content = body, PartialResultMessage = partial });
        var name = contentOnly ? "list_flow_runs" : "get_recent_log";

        var mcp = await dispatcher.CallAsync(name, null, CancellationToken.None);
        var application = await dispatcher.InvokeAsync(name, null, CopilotApplicationCapabilityCaller.InAppAgent, CancellationToken.None);

        var expectedBody = contentOnly ? body : summary + Environment.NewLine + body;
        var expectedPartial = partial.Length == 0 ? string.Empty : "Bounded evidence: password=<redacted>";
        Assert.True(mcp.Success, mcp.Text);
        Assert.True(application.Success, application.Content);
        Assert.Equal(expectedBody, mcp.Text);
        Assert.Equal(expectedBody, application.Content);
        Assert.Equal(expectedPartial, mcp.PartialResultMessage);
        Assert.Equal(expectedPartial, application.PartialResultMessage);
        Assert.DoesNotContain("synthetic-private-value", mcp.PartialResultMessage, StringComparison.Ordinal);
        Assert.NotSame(mcp, CopilotMcpToolResultContract.Capture(name, mcp));
        Assert.False(mcp.RequiresApproval);
        Assert.Equal(CopilotToolFailureKind.None, mcp.FailureKind);
    }

    [Theory]
    [InlineData(false, "success-null")]
    [InlineData(false, "failure-null")]
    [InlineData(false, "failure-partial")]
    [InlineData(true, "success-null")]
    [InlineData(true, "failure-null")]
    [InlineData(true, "failure-partial")]
    public async Task CapabilityAdapterRejectsInvalidPartialMetadataBeforeFactoriesCanDiscardIt(bool contentOnly, string violation)
    {
        var producer = new CopilotCapabilityResult
        {
            Success = violation == "success-null", Summary = "PRIVATE_SUMMARY", Content = "PRIVATE_BODY", ErrorMessage = "PRIVATE_ERROR",
            FailureKind = violation == "success-null" ? CopilotToolFailureKind.None : CopilotToolFailureKind.Authorization,
            PartialResultMessage = violation == "failure-partial" ? "PRIVATE_PARTIAL" : null!,
        };
        var dispatcher = Dispatcher(producer);
        var name = contentOnly ? "list_flow_runs" : "get_recent_log";

        var result = await dispatcher.CallAsync(name, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotMcpToolResultContract.InvalidOutputFailureCode, result.ErrorCode);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.DoesNotContain("PRIVATE_", result.Text, StringComparison.Ordinal);
        Assert.Empty(result.PartialResultMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClassifiedFailuresRetainTheirExistingMeaningAndDoNotBecomePartialSuccess(bool contentOnly)
    {
        var dispatcher = Dispatcher(new CopilotCapabilityResult
        {
            Success = false, Summary = "Summary", Content = "Content", ErrorMessage = "Read denied.", FailureKind = CopilotToolFailureKind.Authorization,
        });
        var result = await dispatcher.CallAsync(contentOnly ? "list_flow_runs" : "get_recent_log", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
        Assert.Equal(contentOnly ? "flow_run_query_failed" : "log_unavailable", result.ErrorCode);
        Assert.Equal("Read denied.", result.Text);
        Assert.Empty(result.PartialResultMessage);
    }

    private static CopilotMcpToolDispatcher Dispatcher(CopilotCapabilityResult result) => new(new CopilotMcpToolEnvironment
    {
        WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
        FlowRunQueryProvider = (_, _, _, _) => Task.FromResult(result),
        RecentLogProvider = (_, _, _, _, _) => Task.FromResult(result),
    });
}
