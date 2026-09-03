using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexExecPolicyTests
{
    [Fact]
    public void EvaluatorUsesMostRestrictiveDecisionAcrossAllShellSegments()
    {
        var request = CreateRequest(
        [
            CreateRule(["git"], CopilotCodexExecPolicyDecision.Allow, order: 0),
            CreateRule(["dotnet", "test"], CopilotCodexExecPolicyDecision.Prompt, order: 1),
            CreateRule(["git", "reset", "--hard"], CopilotCodexExecPolicyDecision.Forbidden, order: 2),
        ]);

        Assert.Equal(
            CopilotCodexExecPolicyDecision.Allow,
            Evaluate(request, "git status; git diff --stat").Decision);
        Assert.Equal(
            CopilotCodexExecPolicyDecision.Prompt,
            Evaluate(request, "git status; dotnet test .\\Test\\ColorVision.UI.Tests").Decision);
        Assert.Equal(
            CopilotCodexExecPolicyDecision.Forbidden,
            Evaluate(request, "dotnet test; git reset --hard").Decision);
        Assert.Equal(
            CopilotCodexExecPolicyDecision.NoMatch,
            Evaluate(request, "git status; unknown-command").Decision);
    }

    [Theory]
    [InlineData("git status; $(Get-Command git)")]
    [InlineData("git status | Out-Null")]
    [InlineData("git status > result.txt")]
    public void DynamicOrPartiallyUnmatchedPowerShellCannotReuseAllowRule(string command)
    {
        var request = CreateRequest(
            [CreateRule(["git", "status"], CopilotCodexExecPolicyDecision.Allow, order: 0)]);

        var evaluation = Evaluate(request, command);

        Assert.Equal(CopilotCodexExecPolicyDecision.NoMatch, evaluation.Decision);
    }

    [Fact]
    public async Task PromptRuleUsesGranularRulesCategoryForPermissionHooks()
    {
        var policy = CopilotCodexApprovalPolicy.CreateGranular(
            sandboxApproval: false,
            rules: true,
            mcpElicitations: false,
            requestPermissions: false,
            skillApproval: false);
        var request = CreateRequest(
            Array.Empty<CopilotCodexExecPolicyRule>(),
            policy);
        var tool = new CopilotShellCommandTool();
        var hook = new RecordingPermissionHook();
        var executor = new CopilotToolExecutor([hook]);
        var withoutOverride = await executor.EvaluatePermissionRequestAsync(
            CreateInvocation(tool, request, categoryOverride: null),
            CancellationToken.None);
        var withOverride = await executor.EvaluatePermissionRequestAsync(
            CreateInvocation(tool, request, CopilotApprovalPromptCategory.Rules),
            CancellationToken.None);

        Assert.False(withoutOverride.Decision.ShouldPrompt);
        Assert.Equal("codex_approval_prompt_disabled", withoutOverride.Decision.FailureCode);
        Assert.True(withOverride.Decision.ShouldPrompt);
        Assert.Contains("Rule requires exact approval", withOverride.Decision.Reason, StringComparison.Ordinal);
        Assert.Equal(1, hook.PermissionRequestCount);
    }

    private static CopilotCodexExecPolicyEvaluation Evaluate(
        CopilotAgentRequest request,
        string command) => CopilotCodexExecPolicyEvaluator.Evaluate(
            request,
            new CopilotShellCommandTool(),
            CreateShellInput(command));

    private static CopilotAgentRequest CreateRequest(
        IReadOnlyList<CopilotCodexExecPolicyRule> rules,
        CopilotCodexApprovalPolicy? approvalPolicy = null) => new()
        {
            ConversationId = "exec-policy-conversation",
            TaskId = "exec-policy-task",
            WorkspacePath = Path.GetFullPath(Path.GetTempPath()),
            UserText = "Run the requested command.",
            TaskIntentText = "Run the requested command.",
            Mode = CopilotAgentMode.Code,
            PreferredShell = CopilotShellKind.PowerShell,
            SearchRootPaths = [Path.GetFullPath(Path.GetTempPath())],
            WritableLocalRootPaths = [Path.GetFullPath(Path.GetTempPath())],
            CodexExecPolicyRules = rules.Select(rule => rule.CreateSnapshot()).ToArray(),
            CodexApprovalPolicy = approvalPolicy ?? CopilotCodexApprovalPolicy.Unspecified,
        };

    private static CopilotCodexExecPolicyRule CreateRule(
        IReadOnlyList<string> pattern,
        CopilotCodexExecPolicyDecision decision,
        int order) => new(
            "default.rules",
            CopilotProjectInstructionConfigSources.CodexHome,
            pattern.Select(token => new CopilotCodexExecPolicyPatternElement([token])).ToArray(),
            decision,
            string.Empty,
            order);

    private static CopilotAgentToolInput CreateShellInput(string command) => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["shell"] = "powershell",
            ["workingDirectory"] = Path.GetFullPath(Path.GetTempPath()),
            ["timeoutSeconds"] = 60,
        },
    };

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        CopilotAgentRequest request,
        CopilotApprovalPromptCategory? categoryOverride) => new()
        {
            CallId = $"exec-policy-{Guid.NewGuid():N}",
            Round = 1,
            RuntimeName = "exec-policy-test",
            Tool = tool,
            AgentRequest = request,
            ToolInput = CreateShellInput("dotnet test"),
            ApprovalPromptCategoryOverride = categoryOverride,
            ApprovalPromptReasonOverride = "Rule requires exact approval.",
        };


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
}
