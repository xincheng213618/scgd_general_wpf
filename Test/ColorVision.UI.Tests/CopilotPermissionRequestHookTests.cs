using ColorVision.Copilot;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotPermissionRequestHookTests
{
    [Fact]
    public async Task ModulePermissionHookCanDenyExactCallBeforeNativeApproval()
    {
        var extensionRegistry = new CopilotAgentExtensionRegistry();
        var hookRegistry = new CopilotToolExecutionHookRegistry();
        using var bridge = new CopilotAgentExtensionBridge(
            extensionRegistry,
            new CopilotCapabilityCatalog(),
            reservedToolNames: [],
            hookRegistry);
        var hook = new DenyingModulePermissionHook();
        using var extensionRegistration = extensionRegistry.Register(
            new CopilotAgentExtensionRegistration
            {
                SourceId = "test.permission-extension",
                SourceName = "Permission test extension",
                SourceVersion = "1.0.0",
                ToolExecutionHooks = [hook],
            });
        var tool = new ProtectedRecordingTool();
        var invocation = CreateInvocation(tool, "module-permission-denial");
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor(hookRegistry)
            .EvaluatePermissionRequestAsync(invocation, CancellationToken.None, events.Add);

        Assert.False(outcome.Decision.ShouldPrompt);
        Assert.Equal("module_permission_denied", outcome.Decision.FailureCode);
        Assert.Equal("module-permission-denial", hook.Context?.CallId);
        Assert.Equal("exact value", hook.Context?.Arguments["query"]);
        Assert.False(hook.Context?.FrameworkApprovalGranted);
        var run = Assert.Single(outcome.HookRuns);
        Assert.Equal("extension:test.permission-extension:hook:permission_policy", run.SourceId);
        Assert.Equal(CopilotToolExecutionHookPhase.PermissionRequest, run.Phase);
        Assert.Equal(CopilotToolExecutionHookState.Denied, run.State);
        Assert.Equal("module_permission_denied", run.FailureCode);
        Assert.Equal(
            [CopilotAgentEventType.HookStarted, CopilotAgentEventType.HookCompleted],
            events.Select(item => item.Type));
        Assert.Equal(run.SourceId, events[0].ToolExecutionHook?.SourceId);
        Assert.Equal(run.FailureCode, events[1].ToolExecutionHook?.Result?.FailureCode);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Fact]
    public async Task PermissionEvidenceAndFrozenHooksCarryIntoApprovedExecution()
    {
        var hookRegistry = new CopilotToolExecutionHookRegistry();
        var hook = new RecordingPermissionHook();
        using var registration = hookRegistry.Register(
            "test:permission",
            hook,
            "^ProtectedRecordingTool$");
        var executor = new CopilotToolExecutor(hookRegistry);
        var tool = new ProtectedRecordingTool();
        var invocation = CreateInvocation(tool, "permission-approved");

        var permission = await executor.EvaluatePermissionRequestAsync(
            invocation,
            CancellationToken.None);
        Assert.True(permission.Decision.ShouldPrompt);
        registration.Dispose();

        var events = new List<CopilotAgentEvent>();
        var outcome = await executor.ExecuteAsync(
            CopyForApprovedExecution(invocation, permission),
            events.Add,
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(1, hook.PermissionCount);
        Assert.Equal(1, hook.BeforeCount);
        Assert.Equal(1, hook.AfterCount);
        Assert.Equal(
            [
                "PermissionRequest:test:permission:Completed",
                "BeforeExecute:builtin:write-tool-policy:Completed",
                "BeforeExecute:test:permission:Completed",
                "AfterExecute:builtin:write-tool-policy:Completed",
                "AfterExecute:test:permission:Completed",
            ],
            outcome.HookRuns.Select(run => $"{run.Phase}:{run.SourceId}:{run.State}"));
        var terminal = Assert.Single(
            events,
            item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(outcome.HookRuns, terminal.ToolExecutionHookRuns);
        var audit = Assert.Single(
            CopilotToolExecutionAuditLogger.GetRecentEntries(),
            item => item.CallId == "permission-approved");
        Assert.Contains(
            "permission:test:permission=completed/",
            audit.HookSummary,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PermissionHookFailureClosesApprovalBoundary()
    {
        var hook = new RecordingPermissionHook(failPermissionRequest: true);
        var executor = new CopilotToolExecutor([hook]);

        var outcome = await executor.EvaluatePermissionRequestAsync(
            CreateInvocation(new ProtectedRecordingTool(), "permission-failure"),
            CancellationToken.None);

        Assert.False(outcome.Decision.ShouldPrompt);
        Assert.Equal("permission_hook_failed", outcome.Decision.FailureCode);
        var run = Assert.Single(outcome.HookRuns);
        Assert.Equal(CopilotToolExecutionHookPhase.PermissionRequest, run.Phase);
        Assert.Equal(CopilotToolExecutionHookState.Failed, run.State);
        Assert.Equal("permission_hook_failed", run.FailureCode);
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        string callId)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            RuntimeName = "permission-hook-test",
            Tool = tool,
            AgentRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Code,
                UserText = "Run the protected recording tool.",
                TaskIntentText = "Run the protected recording tool.",
            },
            ToolInput = new CopilotAgentToolInput
            {
                Query = "exact value",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = "exact value",
                },
            },
        };
    }

    private static CopilotToolInvocation CopyForApprovedExecution(
        CopilotToolInvocation invocation,
        CopilotToolPermissionRequestOutcome permission)
    {
        return new CopilotToolInvocation
        {
            CallId = invocation.CallId,
            RuntimeName = invocation.RuntimeName,
            Tool = invocation.Tool,
            AgentRequest = invocation.AgentRequest,
            ToolInput = invocation.ToolInput,
            FrameworkApprovalGranted = true,
            InitialHookRuns = permission.HookRuns,
            InitialHookBindings = permission.HookBindings,
        };
    }

    private sealed class DenyingModulePermissionHook
        : ICopilotModuleToolPermissionRequestHook
    {
        public string Name => "Permission_Policy";

        public string ToolNamePattern => "^ProtectedRecordingTool$";

        public CopilotModuleToolExecutionHookContext? Context { get; private set; }

        public Task<CopilotModuleToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotModuleToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Context = context;
            return Task.FromResult(CopilotModuleToolPermissionRequestDecision.Deny(
                "The module permission policy denied this exact call.",
                "Module Permission Denied"));
        }

        public Task<CopilotModuleToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotModuleToolExecutionHookContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(CopilotModuleToolExecutionHookDecision.Proceed);
    }

    private sealed class RecordingPermissionHook(bool failPermissionRequest = false)
        : ICopilotToolPermissionRequestHook
    {
        public int PermissionCount { get; private set; }

        public int BeforeCount { get; private set; }

        public int AfterCount { get; private set; }

        public Task<CopilotToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PermissionCount++;
            if (failPermissionRequest)
                throw new InvalidOperationException("Permission hook failure details must not escape.");
            return Task.FromResult(CopilotToolPermissionRequestDecision.Prompt);
        }

        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeforeCount++;
            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AfterCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ProtectedRecordingTool : ICopilotFrameworkApprovedTool
    {
        public string Name => "ProtectedRecordingTool";

        public string Description => "Records an approved protected execution.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.NonIdempotent);

        public int ExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The protected tool requires framework approval.");

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Protected recording tool completed.",
            });
        }
    }
}
