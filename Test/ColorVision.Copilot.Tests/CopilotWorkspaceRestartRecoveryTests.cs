using ColorVision.Copilot;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceRestartRecoveryTests
{
    [Theory]
    [InlineData(CopilotToolExecutionState.Running, CopilotToolAccess.Write, CopilotToolIdempotency.Idempotent, true)]
    [InlineData(CopilotToolExecutionState.Running, CopilotToolAccess.ReadOnly, CopilotToolIdempotency.Idempotent, false)]
    [InlineData(CopilotToolExecutionState.Running, CopilotToolAccess.ReadOnly, CopilotToolIdempotency.Unknown, true)]
    [InlineData(CopilotToolExecutionState.Pending, CopilotToolAccess.Write, CopilotToolIdempotency.Unknown, false)]
    [InlineData(CopilotToolExecutionState.AwaitingApproval, CopilotToolAccess.Write, CopilotToolIdempotency.Unknown, false)]
    public void RecoveryDistinguishesUnconfirmedExecutionFromUndispatchedOrReadOnlyWork(
        CopilotToolExecutionState state, CopilotToolAccess access, CopilotToolIdempotency idempotency, bool unknown)
    {
        var trace = new CopilotAgentTraceEntry
        {
            State = state, Access = access, Idempotency = idempotency, RetryEligible = true,
            ToolName = "ApplyWorkspacePatchEnvelope", StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            WorkspaceRecheckPaths = [Path.Combine(Path.GetTempPath(), "pending.txt")],
        };
        trace.EnsureValid(DateTimeOffset.UtcNow);
        Assert.Equal(CopilotToolExecutionState.Interrupted, trace.State);
        Assert.Equal(unknown, trace.FailureKind == CopilotToolFailureKind.OutcomeUnknown);
        Assert.Equal(unknown, trace.FailureCode == CopilotToolFailureCode.OutcomeUnknown);
        Assert.False(trace.RetryEligible);
        Assert.Equal(state == CopilotToolExecutionState.Running && access == CopilotToolAccess.Write, trace.WorkspaceRecheckPaths.Count > 0);
    }

    [Fact]
    public void ReloadPreservesLegacyWarningsAndNormalizesOnlyBoundedNativeWritePaths()
    {
        var path = Path.Combine(Path.GetTempPath(), "pending.txt");
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "interrupted")
        {
            WorkspaceDiffWarning = "原有待核查文件：legacy.txt",
        };
        message.AgentTraceEntries.Add(new()
        {
            ToolName = "ApplyWorkspacePatchEnvelope", State = CopilotToolExecutionState.Running, Access = CopilotToolAccess.Write,
            WorkspaceRecheckPaths = ["relative.txt", "C:\\bad\0path", path, path.ToUpperInvariant(), .. Enumerable.Range(0, 20).Select(i => Path.Combine(Path.GetTempPath(), $"extra-{i}.txt"))],
        });
        message.EnsureValid();
        Assert.Equal(8, Assert.Single(message.AgentTraceEntries).WorkspaceRecheckPaths.Count);
        Assert.Contains("pending.txt", message.WorkspaceDiffWarning, StringComparison.Ordinal);
        Assert.Contains("legacy.txt", message.WorkspaceDiffWarning, StringComparison.Ordinal);
        Assert.DoesNotContain("relative.txt", message.WorkspaceDiffWarning, StringComparison.Ordinal);
        var warning = message.WorkspaceDiffWarning;
        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(JsonConvert.SerializeObject(message))!;
        restored.EnsureValid();
        Assert.Equal(warning, restored.WorkspaceDiffWarning);
    }

    [Theory]
    [InlineData("ApplyWorkspacePatchEnvelope", CopilotToolExecutionState.Completed)]
    [InlineData("OtherWriteTool", CopilotToolExecutionState.Running)]
    public void CompletedOrUnrecognizedToolsCannotRestoreWorkspacePathAuthority(string toolName, CopilotToolExecutionState state)
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "old result");
        message.AgentTraceEntries.Add(new()
        {
            ToolName = toolName, State = state, Access = CopilotToolAccess.Write,
            WorkspaceRecheckPaths = [Path.Combine(Path.GetTempPath(), "pending.txt")],
        });
        message.EnsureValid();
        Assert.Empty(Assert.Single(message.AgentTraceEntries).WorkspaceRecheckPaths);
        Assert.False(message.HasWorkspaceDiffWarning);
    }
}
