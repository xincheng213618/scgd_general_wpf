using ColorVision.Copilot;
using ModelContextProtocol.Protocol;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexApprovalPolicyTests
{
    [Fact]
    public async Task UntrustedPolicyPromotesEveryWriteToolToExactCallApproval()
    {
        var tool = new RecordingWriteTool("WriteProbe");
        var request = CreateRequest(CopilotCodexApprovalPolicy.CreateScalar(
            CopilotCodexApprovalPolicyMode.Untrusted));
        var executor = new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>());
        var bridge = CreateBridge(request, tool, executor);

        Assert.True(CopilotCodexApprovalPolicySelection.RequiresNativeApproval(
            request.CodexApprovalPolicy,
            tool));
        Assert.Contains(
            "ApprovalRequired",
            Assert.Single(bridge.CreateFunctions()).GetType().Name,
            StringComparison.Ordinal);

        var permission = await executor.EvaluatePermissionRequestAsync(
            CreateInvocation(tool, request, "untrusted-permission", frameworkApprovalGranted: false),
            CancellationToken.None);

        var denied = await executor.ExecuteAsync(
            CreateInvocation(tool, request, "untrusted-denied", frameworkApprovalGranted: false),
            _ => { },
            CancellationToken.None);
        var approved = await executor.ExecuteAsync(
            CreateInvocation(tool, request, "untrusted-approved", frameworkApprovalGranted: true),
            _ => { },
            CancellationToken.None);
        string harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            [tool],
            new CopilotAgentEnvironmentContext(),
            taskLedgerEnabled: false,
            agentModeEnabled: true);

        Assert.Equal(CopilotToolExecutionState.Denied, denied.Execution.State);
        Assert.True(permission.Decision.ShouldPrompt);
        Assert.Contains("approval_policy=untrusted", permission.Decision.Reason, StringComparison.Ordinal);
        Assert.Equal(CopilotToolFailureKind.Authorization, denied.Result.FailureKind);
        Assert.Equal(CopilotToolExecutionState.Completed, approved.Execution.State);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Contains("approval_policy=untrusted is frozen", harness, StringComparison.Ordinal);
        Assert.Contains("every write-capable", harness, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledPromptPoliciesFailClosedBeforeHooksOrExecution()
    {
        var policies = new[]
        {
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Never),
            CopilotCodexApprovalPolicy.CreateGranular(
                sandboxApproval: false,
                rules: true,
                mcpElicitations: true,
                requestPermissions: false,
                skillApproval: false),
        };
        foreach (var policy in policies)
        {
            var tool = new RecordingProtectedWriteTool("ProtectedProbe");
            var request = CreateRequest(policy);
            var hook = new RecordingPermissionHook();
            var executor = new CopilotToolExecutor([hook]);
            var invocation = CreateInvocation(
                tool,
                request,
                $"disabled-{policy.Mode}",
                frameworkApprovalGranted: false);

            var permission = await executor.EvaluatePermissionRequestAsync(
                invocation,
                CancellationToken.None);
            var execution = await executor.ExecuteAsync(
                invocation,
                _ => { },
                CancellationToken.None);

            Assert.False(permission.Decision.ShouldPrompt);
            Assert.Equal("codex_approval_prompt_disabled", permission.Decision.FailureCode);
            Assert.Empty(permission.HookRuns);
            Assert.Equal(0, hook.PermissionRequestCount);
            Assert.Equal(CopilotToolExecutionState.Denied, execution.Execution.State);
            Assert.Equal("codex_approval_prompt_disabled", execution.Result.FailureCode);
            Assert.Equal(0, tool.ExecutionCount);
        }
    }

    [Fact]
    public async Task OnRequestAndEnabledGranularPoliciesPreserveNativeApproval()
    {
        var policies = new[]
        {
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            CopilotCodexApprovalPolicy.CreateGranular(
                sandboxApproval: true,
                rules: false,
                mcpElicitations: true,
                requestPermissions: false,
                skillApproval: false),
        };
        foreach (var policy in policies)
        {
            var tool = new RecordingProtectedWriteTool("ProtectedProbe");
            var request = CreateRequest(policy);
            var hook = new RecordingPermissionHook();
            var permission = await new CopilotToolExecutor([hook])
                .EvaluatePermissionRequestAsync(
                    CreateInvocation(tool, request, $"prompt-{policy.Mode}", false),
                    CancellationToken.None);

            Assert.True(permission.Decision.ShouldPrompt);
            Assert.Equal(1, hook.PermissionRequestCount);
        }
    }

    [Fact]
    public async Task GranularPromptCategoriesAreEnforcedIndependently()
    {
        var categories = Enum.GetValues<CopilotApprovalPromptCategory>();
        foreach (var enabledCategory in categories)
        {
            var policy = CreateSingleCategoryPolicy(enabledCategory);
            var request = CreateRequest(policy);
            var hook = new RecordingPermissionHook();
            var executor = new CopilotToolExecutor([hook]);
            var allowedTool = new RecordingProtectedWriteTool(
                $"Allowed{enabledCategory}",
                enabledCategory);
            var disabledCategory = categories[(Array.IndexOf(categories, enabledCategory) + 1) % categories.Length];
            var deniedTool = new RecordingProtectedWriteTool(
                $"Denied{disabledCategory}",
                disabledCategory);

            var allowed = await executor.EvaluatePermissionRequestAsync(
                CreateInvocation(allowedTool, request, $"allowed-{enabledCategory}", false),
                CancellationToken.None);
            var denied = await executor.EvaluatePermissionRequestAsync(
                CreateInvocation(deniedTool, request, $"denied-{disabledCategory}", false),
                CancellationToken.None);

            Assert.True(allowed.Decision.ShouldPrompt);
            Assert.True(CopilotCodexApprovalPolicySelection.AllowsAutomaticReview(
                policy,
                enabledCategory));
            Assert.False(denied.Decision.ShouldPrompt);
            Assert.Equal("codex_approval_prompt_disabled", denied.Decision.FailureCode);
            Assert.Contains(GetConfigCategoryName(disabledCategory), denied.Decision.Reason, StringComparison.Ordinal);
            Assert.False(CopilotCodexApprovalPolicySelection.AllowsAutomaticReview(
                policy,
                disabledCategory));
            Assert.Equal(1, hook.PermissionRequestCount);
        }
    }

    [Fact]
    public void ExternalMcpApprovalUsesMcpElicitationsCategory()
    {
        var protectedCapability = CopilotMcpClientCapabilityPolicy.Create(
            CopilotMcpClientAccessPolicy.RequireApproval,
            TimeSpan.FromSeconds(15));
        var readOnlyCapability = CopilotMcpClientCapabilityPolicy.Create(
            CopilotMcpClientAccessPolicy.ReadOnly,
            TimeSpan.FromSeconds(15));

        Assert.True(protectedCapability.RequiresNativeApproval);
        Assert.Equal(
            CopilotApprovalPromptCategory.McpElicitations,
            protectedCapability.ApprovalPromptCategory);
        Assert.False(readOnlyCapability.RequiresNativeApproval);
    }

    [Fact]
    public void DestructiveMcpAnnotationAlwaysRequiresApproval()
    {
        var capability = CopilotMcpClientCapabilityPolicy.Create(
            CopilotMcpClientAccessPolicy.ReadOnly,
            TimeSpan.FromSeconds(15),
            new ToolAnnotations
            {
                DestructiveHint = true,
                ReadOnlyHint = true,
            });

        Assert.True(capability.RequiresNativeApproval);
        Assert.Equal(CopilotToolAccess.Write, capability.Access);
        Assert.Equal(
            CopilotApprovalPromptCategory.McpElicitations,
            capability.ApprovalPromptCategory);
    }

    [Fact]
    public void ApprovalCategoryChangesCapabilityRevision()
    {
        var catalog = new CopilotCapabilityCatalog();
        var sandboxSnapshot = catalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "approval-category-test",
            "Approval category test",
            [new RecordingProtectedWriteTool(
                "CategorizedProbe",
                CopilotApprovalPromptCategory.SandboxApproval)]);
        var mcpSnapshot = catalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "approval-category-test",
            "Approval category test",
            [new RecordingProtectedWriteTool(
                "CategorizedProbe",
                CopilotApprovalPromptCategory.McpElicitations)]);

        var sandboxEntry = Assert.Single(sandboxSnapshot.Capabilities);
        var mcpEntry = Assert.Single(mcpSnapshot.Capabilities);
        Assert.Equal(CopilotApprovalPromptCategory.SandboxApproval, sandboxEntry.ApprovalPromptCategory);
        Assert.Equal(CopilotApprovalPromptCategory.McpElicitations, mcpEntry.ApprovalPromptCategory);
        Assert.NotEqual(sandboxEntry.Fingerprint, mcpEntry.Fingerprint);
        Assert.Equal(sandboxEntry.Revision + 1, mcpEntry.Revision);
    }

    private sealed class RecordingWriteTool : ICopilotTool
    {
        private int _executionCount;

        public RecordingWriteTool(string name)
        {
            Name = name;
            Capability = new CopilotToolCapabilityDescriptor
            {
                Access = CopilotToolAccess.Write,
                RiskLevel = CopilotToolRiskLevel.Medium,
                ApprovalMode = CopilotToolApprovalMode.Never,
                Idempotency = CopilotToolIdempotency.NonIdempotent,
                ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                EvidenceMode = CopilotToolEvidenceMode.None,
            };
        }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public string Name { get; }

        public string Description => "Records exact approval-policy execution.";

        public CopilotToolCapabilityDescriptor Capability { get; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Write tool executed.",
            });
        }
    }

    private sealed class RecordingProtectedWriteTool : ICopilotFrameworkApprovedTool
    {
        private int _executionCount;

        public RecordingProtectedWriteTool(
            string name,
            CopilotApprovalPromptCategory approvalPromptCategory = CopilotApprovalPromptCategory.SandboxApproval)
        {
            Name = name;
            Capability = CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.NonIdempotent,
                approvalPromptCategory: approvalPromptCategory);
        }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public string Name { get; }

        public string Description => "Records exact protected approval-policy execution.";

        public CopilotToolCapabilityDescriptor Capability { get; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Approval is required.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Approved tool executed.",
            });
        }
    }

    private sealed class RecordingPermissionHook : ICopilotToolPermissionRequestHook
    {
        private int _permissionRequestCount;

        public int PermissionRequestCount => Volatile.Read(ref _permissionRequestCount);

        public Task<CopilotToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _permissionRequestCount);
            return Task.FromResult(CopilotToolPermissionRequestDecision.Prompt);
        }

        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(CopilotToolExecutionHookDecision.Proceed);

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static CopilotAgentRequest CreateRequest(CopilotCodexApprovalPolicy policy) => new()
    {
        ConversationId = "approval-policy-conversation",
        TaskId = "approval-policy-task",
        WorkspacePath = Path.GetTempPath(),
        UserText = "Change the requested application state.",
        TaskIntentText = "Change the requested application state.",
        Mode = CopilotAgentMode.Code,
        CodexApprovalPolicy = policy,
        WritableLocalRootPaths = [Path.GetTempPath()],
    };

    private static CopilotCodexApprovalPolicy CreateSingleCategoryPolicy(
        CopilotApprovalPromptCategory category) =>
        CopilotCodexApprovalPolicy.CreateGranular(
            sandboxApproval: category == CopilotApprovalPromptCategory.SandboxApproval,
            rules: category == CopilotApprovalPromptCategory.Rules,
            mcpElicitations: category == CopilotApprovalPromptCategory.McpElicitations,
            requestPermissions: category == CopilotApprovalPromptCategory.RequestPermissions,
            skillApproval: category == CopilotApprovalPromptCategory.SkillApproval);

    private static string GetConfigCategoryName(CopilotApprovalPromptCategory category) => category switch
    {
        CopilotApprovalPromptCategory.SandboxApproval => "sandbox_approval",
        CopilotApprovalPromptCategory.Rules => "rules",
        CopilotApprovalPromptCategory.McpElicitations => "mcp_elicitations",
        CopilotApprovalPromptCategory.RequestPermissions => "request_permissions",
        CopilotApprovalPromptCategory.SkillApproval => "skill_approval",
        _ => string.Empty,
    };

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        CopilotAgentRequest request,
        string callId,
        bool frameworkApprovalGranted) => new()
        {
            CallId = callId,
            Round = 1,
            RuntimeName = "approval-policy-test",
            Tool = tool,
            AgentRequest = request,
            ToolInput = CopilotAgentToolInput.Empty,
            FrameworkApprovalGranted = frameworkApprovalGranted,
        };

    private static CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge CreateBridge(
        CopilotAgentRequest request,
        ICopilotTool tool,
        CopilotToolExecutor executor) => new(
            request,
            CopilotExecutionScope.ForAgentRun(request),
            [tool],
            maxToolCalls: 2,
            executor,
            new CopilotFrameworkApprovalCoordinator(),
            _ => { },
            capabilityRevisionProvider: () => 1);

}
