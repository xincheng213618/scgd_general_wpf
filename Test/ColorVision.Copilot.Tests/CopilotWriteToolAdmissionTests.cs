using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWriteToolAdmissionTests
{
    [Theory]
    [InlineData(CopilotAgentMode.Diagnose, "Check the current application state.", "request_write_access_denied")]
    [InlineData(CopilotAgentMode.Auto, "只读审计，不要修改任何文件。", "request_write_access_denied")]
    [InlineData(CopilotAgentMode.Code, "Read-only inspection; do not edit any file.", "request_write_access_denied")]
    [InlineData(CopilotAgentMode.Plan, "Update the application.", "plan_mode_write_denied")]
    [InlineData(CopilotAgentMode.Review, "Review and update the application.", "review_mode_write_denied")]
    public async Task HiddenWritesAreAlsoRejectedAtTheExecutorBoundary(
        CopilotAgentMode mode, string prompt, string failureCode)
    {
        foreach (var protectedWrite in new[] { false, true })
        {
            var tool = new RecordingTool(protectedWrite);
            var request = new CopilotAgentRequest
            {
                Mode = mode,
                UserText = prompt,
                CodexSandboxMode = CopilotCodexSandboxMode.DangerFullAccess,
            };
            Assert.Empty(new CopilotToolRegistry([tool]).FindTools(request));

            var events = new List<CopilotAgentEvent>();
            var outcome = await new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
                new CopilotToolInvocation
                {
                    CallId = "injected-write",
                    Tool = tool,
                    AgentRequest = request,
                    FrameworkApprovalGranted = protectedWrite,
                },
                events.Add,
                CancellationToken.None);

            Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
            Assert.Equal(failureCode, outcome.Result.FailureCode);
            Assert.Equal(0, tool.ExecutionCount);
            var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
            Assert.Equal(failureCode, terminal.ToolResult!.FailureCode);
        }
    }

    [Fact]
    public async Task WritableRequestStillReachesTheApprovedExecutionPath()
    {
        var tool = new RecordingTool(protectedWrite: true);
        var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Auto, UserText = "Update the application." };
        Assert.Same(tool, Assert.Single(new CopilotToolRegistry([tool]).FindTools(request)));

        var outcome = await new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "approved-write",
                Tool = tool,
                AgentRequest = request,
                FrameworkApprovalGranted = true,
            },
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success, outcome.Result.ErrorMessage);
        Assert.Equal(1, tool.ExecutionCount);
    }

    private sealed class RecordingTool(bool protectedWrite) : ICopilotFrameworkApprovedTool
    {
        public string Name => "RecordingWrite";
        public string Description => "Records entry into a write-capable tool body.";
        public int ExecutionCount { get; private set; }
        public CopilotToolCapabilityDescriptor Capability { get; } = protectedWrite
            ? CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent)
            : new CopilotToolCapabilityDescriptor
            {
                Access = CopilotToolAccess.Write,
                RiskLevel = CopilotToolRiskLevel.Medium,
                ApprovalMode = CopilotToolApprovalMode.Never,
                Idempotency = CopilotToolIdempotency.NonIdempotent,
                ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                EvidenceMode = CopilotToolEvidenceMode.None,
            };
        public bool CanHandle(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Executed." });
        }
        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken) =>
            ExecuteAsync(request, input, cancellationToken);
    }
}
