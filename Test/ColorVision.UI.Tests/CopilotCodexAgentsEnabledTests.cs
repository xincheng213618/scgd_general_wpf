using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexAgentsEnabledTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenIntoSubmittedAndQueuedTurnSnapshots()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [agents]
                enabled = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "agents.enabled = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Use only DelegateExplore; do not use parent agent file tools.",
                CopilotAgentMode.Auto,
                submittedContext);
            File.WriteAllText(projectConfigPath, "agents.enabled = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                "Use only DelegateExplore; do not use parent agent file tools.",
                CopilotAgentMode.Auto,
                refreshedContext);
            var queuedFollowUp = new CopilotQueuedFollowUp(
                "run-1",
                "conversation-1",
                "Conversation",
                "Continue the work.",
                CopilotAgentMode.Code,
                CopilotProfileConfig.CreateDefault(),
                submittedContext);
            var queuedContext = queuedFollowUp.CreateExecutionContext(
                CopilotConversationHistorySnapshot.Empty);

            Assert.False(submittedContext.ProjectInstructionDiscoveryOptions.ConfiguredAgentsEnabled);
            Assert.True(submittedContext.ProjectInstructionDiscoveryOptions.HasAgentsEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submittedContext.ProjectInstructionDiscoveryOptions.AgentsEnabledSource);
            Assert.False(submittedPlan.CodexAgentsEnabled);
            Assert.False(submittedPlan.RequiresDelegatedWorkspaceEvidence);
            Assert.True(refreshed.ConfiguredAgentsEnabled);
            Assert.True(refreshedPlan.CodexAgentsEnabled);
            Assert.True(refreshedPlan.RequiresDelegatedWorkspaceEvidence);
            Assert.False(queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredAgentsEnabled);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndInvalidValuesCannotBroadenTheCodexHomeContract()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [agents]
                enabled = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[agents]\nenabled = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredAgentsEnabled);
            Assert.True(untrusted.HasAgentsEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.AgentsEnabledSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[agents]\nenabled = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(invalid.HasAgentsEnabledOverride);
            Assert.True(invalid.ConfiguredAgentsEnabled);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DisabledSnapshotHidesDelegationToolsAndRejectsInjectedCalls()
    {
        var runner = new RecordingSubagentRunner();
        var delegateTool = new CopilotDelegateExploreTool(runner);
        var registry = new CopilotToolRegistry(
            CopilotToolRegistry.CreateCoreDefaultTools().Append(delegateTool));
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = "Use only DelegateExplore; do not use parent agent file tools.",
            TaskIntentText = "Use only DelegateExplore; do not use parent agent file tools.",
            CodexAgentsEnabled = false,
        };

        var availableTools = registry.FindTools(request);
        var outcome = await new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "stale-delegate-call",
                Round = 1,
                RuntimeName = "codex-agents-test",
                Tool = delegateTool,
                AgentRequest = request,
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["task"] = "Inspect the workspace.",
                    },
                },
            },
            _ => { },
            CancellationToken.None);
        var directResult = await delegateTool.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Inspect the workspace.",
                },
            },
            CancellationToken.None);

        Assert.DoesNotContain(availableTools, tool => tool is CopilotDelegateSubagentTool);
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
        Assert.Equal("codex_agents_disabled", outcome.Result.FailureCode);
        Assert.False(directResult.Success);
        Assert.Equal("codex_agents_disabled", directResult.FailureCode);
        Assert.Equal(0, runner.RunCount);
    }

    [Fact]
    public void DiagnosticsExposeTheEffectiveValueSourceAndFailClosedBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredAgentsEnabled = false,
            HasAgentsEnabledOverride = true,
            AgentsEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexAgentsEnabled = false,
            HasCodexAgentsEnabledOverride = true,
            CodexAgentsEnabledSourceLabel = options.AgentsEnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex agents.enabled：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.AgentsEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("拒绝旧计划", memoryReport, StringComparison.Ordinal);
        Assert.Contains("子代理工具：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("旧调用也会拒绝", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.enabled：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("注入调用", debugReport, StringComparison.Ordinal);
    }

    private sealed class RecordingSubagentRunner : ICopilotSubagentRunner
    {
        public int RunCount { get; private set; }

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new CopilotSubagentResult());
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-agents-enabled-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
