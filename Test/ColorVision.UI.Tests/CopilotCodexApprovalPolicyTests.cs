using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexApprovalPolicyTests
{
    [Fact]
    public void ClosestTrustedGranularPolicyIsParsedAndFrozenIntoTurnSnapshots()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                approval_policy = "untrusted"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """
                approval_policy = { granular = {
                    sandbox_approval = false,
                    rules = true,
                    mcp_elicitations = true,
                    request_permissions = true
                } }
                """);

            var submittedContext = CreateHostContext(globalRoot, projectRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Edit the workspace and run its tests.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });

            File.WriteAllText(projectConfigPath, "approval_policy = \"on-request\"");
            var refreshedContext = CreateHostContext(globalRoot, projectRoot);
            var queuedFollowUp = new CopilotQueuedFollowUp(
                "run-1",
                "conversation-1",
                "Conversation",
                "Continue.",
                CopilotAgentMode.Code,
                CopilotProfileConfig.CreateDefault(),
                submittedContext);
            var queuedContext = queuedFollowUp.CreateExecutionContext(
                CopilotConversationHistorySnapshot.Empty);

            var policy = submittedContext.ProjectInstructionDiscoveryOptions.ConfiguredApprovalPolicy;
            Assert.True(submittedContext.ProjectInstructionDiscoveryOptions.HasApprovalPolicyOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submittedContext.ProjectInstructionDiscoveryOptions.ApprovalPolicySource);
            Assert.Equal(CopilotCodexApprovalPolicyMode.Granular, policy.Mode);
            Assert.False(policy.SandboxApproval);
            Assert.True(policy.Rules);
            Assert.True(policy.McpElicitations);
            Assert.True(policy.RequestPermissions);
            Assert.False(policy.SkillApproval);
            Assert.Same(policy, submittedPlan.CodexApprovalPolicy);
            Assert.Same(policy, submittedRequest.CodexApprovalPolicy);
            Assert.Equal(
                CopilotCodexApprovalPolicyMode.OnRequest,
                refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredApprovalPolicy.Mode);
            Assert.Same(
                policy,
                queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredApprovalPolicy);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndMalformedProjectValuesCannotChangeTheCodexHomePolicy()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                approval_policy = "never"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "approval_policy = \"on-request\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexApprovalPolicyMode.Never, untrusted.ConfiguredApprovalPolicy.Mode);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ApprovalPolicySource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "approval_policy = { granular = { sandbox_approval = true, mcp_elicitations = true } }");
            var malformed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(malformed.HasApprovalPolicyOverride);
            Assert.Equal(CopilotCodexApprovalPolicyMode.Unspecified, malformed.ConfiguredApprovalPolicy.Mode);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

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
    public async Task OnRequestAndEnabledGranularPoliciesPreserveNativeApprovalAndDiagnostics()
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

        var granular = policies[1];
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredApprovalPolicy = granular,
            HasApprovalPolicyOverride = true,
            ApprovalPolicySource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ProfileLabel = "Profile",
            Mode = CopilotAgentMode.Code,
            CodexApprovalPolicy = granular,
            HasCodexApprovalPolicyOverride = true,
            CodexApprovalPolicySourceLabel = options.ApprovalPolicySourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex approval_policy：granular(sandbox_approval=true", memoryReport, StringComparison.Ordinal);
        Assert.Contains("其余 granular 标志已保留", memoryReport, StringComparison.Ordinal);
        Assert.Contains("审批策略：granular(sandbox_approval=true", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex approval_policy：granular(sandbox_approval=true", debugReport, StringComparison.Ordinal);
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

        public RecordingProtectedWriteTool(string name)
        {
            Name = name;
        }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public string Name { get; }

        public string Description => "Records exact protected approval-policy execution.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.NonIdempotent);

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

    private static CopilotAgentHostContextSnapshot CreateHostContext(
        string globalRoot,
        string projectRoot) => new(
            activeDocumentPath: null,
            projectRoot,
            attachments: null,
            liveContext: null,
            conversationHistory: null,
            additionalReadRootPaths: null,
            globalInstructionRootPath: globalRoot);

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

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-approval-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
