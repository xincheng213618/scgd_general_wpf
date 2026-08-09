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
    public void GlobalAndTrustedProjectRulesAreDiscoveredAndFrozenIntoSubmittedTurn()
    {
        string codexHome = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(codexHome, "config.toml"),
                $"[projects.'{projectRoot}']\ntrust_level = \"trusted\"");
            string globalRulesDirectory = Path.Combine(codexHome, "rules");
            Directory.CreateDirectory(globalRulesDirectory);
            string globalRulesPath = Path.Combine(globalRulesDirectory, "default.rules");
            File.WriteAllText(
                globalRulesPath,
                """
                prefix_rule(
                    pattern = ["git", ["status", "diff"]],
                    decision = "allow",
                    justification = "Read-only Git inspection",
                    match = ["git status --short", ["git", "diff", "--stat"]],
                    not_match = ["git log"],
                )
                """);
            string projectRulesDirectory = Path.Combine(projectRoot, ".codex", "rules");
            Directory.CreateDirectory(projectRulesDirectory);
            string projectRulesPath = Path.Combine(projectRulesDirectory, "project.rules");
            File.WriteAllText(
                projectRulesPath,
                """
                prefix_rule(
                    pattern = ["git", "reset", "--hard"],
                    decision = "forbidden",
                    justification = "Do not discard workspace changes",
                )
                """);

            var hostContext = CreateHostContext(codexHome, projectRoot);
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect Git status and preserve the workspace.",
                CopilotAgentMode.Code,
                hostContext);

            var options = hostContext.ProjectInstructionDiscoveryOptions;
            Assert.Equal(2, options.ConfiguredExecPolicyRules.Count);
            Assert.Equal([globalRulesPath, projectRulesPath], options.AppliedExecPolicyFilePaths);
            Assert.Empty(options.ConfiguredExecPolicyIssues);
            Assert.Equal(
                [CopilotProjectInstructionConfigSources.CodexHome, CopilotProjectInstructionConfigSources.TrustedProject],
                options.ConfiguredExecPolicyRules.Select(rule => rule.Source));
            Assert.Equal(2, plan.CodexExecPolicyRules.Count);

            File.WriteAllText(
                globalRulesPath,
                "prefix_rule(pattern=[\"git\", \"status\"], decision=\"forbidden\")");
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(codexHome, projectRoot);

            Assert.Equal(2, request.CodexExecPolicyRules.Count);
            Assert.Equal(
                CopilotCodexExecPolicyDecision.Allow,
                Evaluate(request, "git status --short").Decision);
            Assert.Equal(
                CopilotCodexExecPolicyDecision.Forbidden,
                Evaluate(CreateRequest(refreshed.ConfiguredExecPolicyRules), "git status --short").Decision);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedProjectRulesAreIgnoredWhileGlobalRulesRemainActive()
    {
        string codexHome = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(codexHome, "config.toml"),
                $"[projects.'{projectRoot}']\ntrust_level = \"untrusted\"");
            string globalRulesDirectory = Path.Combine(codexHome, "rules");
            Directory.CreateDirectory(globalRulesDirectory);
            string globalRulesPath = Path.Combine(globalRulesDirectory, "default.rules");
            File.WriteAllText(
                globalRulesPath,
                "prefix_rule(pattern=[\"git\", \"status\"], decision=\"allow\")");
            string projectRulesDirectory = Path.Combine(projectRoot, ".codex", "rules");
            Directory.CreateDirectory(projectRulesDirectory);
            File.WriteAllText(
                Path.Combine(projectRulesDirectory, "project.rules"),
                "prefix_rule(pattern=[\"git\"], decision=\"forbidden\")");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome, projectRoot);

            var rule = Assert.Single(options.ConfiguredExecPolicyRules);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, rule.Source);
            Assert.Equal([globalRulesPath], options.AppliedExecPolicyFilePaths);
            Assert.Equal(
                CopilotCodexExecPolicyDecision.Allow,
                Evaluate(CreateRequest(options.ConfiguredExecPolicyRules), "git status").Decision);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void InvalidSelfTestRejectsOnlyThatRuleAndReportsTheSource()
    {
        string codexHome = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(codexHome, "rules");
            Directory.CreateDirectory(rulesDirectory);
            string rulesPath = Path.Combine(rulesDirectory, "default.rules");
            File.WriteAllText(
                rulesPath,
                """
                prefix_rule(
                    pattern = ["git"],
                    decision = "allow",
                    not_match = ["git status"],
                )
                prefix_rule(pattern = ["dotnet", "test"], decision = "prompt")
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            var rule = Assert.Single(options.ConfiguredExecPolicyRules);
            Assert.Equal("dotnet test", rule.FormatPattern());
            var issue = Assert.Single(options.ConfiguredExecPolicyIssues);
            Assert.Equal(rulesPath, issue.SourceFilePath);
            Assert.Contains("not_match", issue.Message, StringComparison.Ordinal);
            Assert.Equal([rulesPath], options.AppliedExecPolicyFilePaths);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

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

    [Fact]
    public void DiagnosticsExposeRuleFileIssueAndFrozenPrecedenceContract()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            AppliedExecPolicyFilePaths = ["default.rules"],
            ConfiguredExecPolicyRules =
            [
                CreateRule(["git"], CopilotCodexExecPolicyDecision.Allow, order: 0),
            ],
            ConfiguredExecPolicyIssues =
            [
                new CopilotCodexExecPolicyIssue("default.rules", "Invalid example."),
            ],
        };

        string report = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);

        Assert.Contains("Codex exec policy：已加载规则 1 个 / 来源文件 1 个 / 配置问题 1 个", report, StringComparison.Ordinal);
        Assert.Contains("提交时冻结", report, StringComparison.Ordinal);
        Assert.Contains("forbidden > prompt > allow", report, StringComparison.Ordinal);
        Assert.Contains("Codex exec policy：已加载规则 1 个 / 来源文件 1 个 / 配置问题 1 个", memoryReport, StringComparison.Ordinal);
        Assert.Contains("最严格决策优先", memoryReport, StringComparison.Ordinal);
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

    private static CopilotAgentHostContextSnapshot CreateHostContext(
        string codexHome,
        string projectRoot) => new(
            activeDocumentPath: null,
            projectRoot,
            attachments: null,
            liveContext: null,
            conversationHistory: null,
            additionalReadRootPaths: null,
            globalInstructionRootPath: codexHome);

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-exec-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
}
