using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

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
                max_threads = 3

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                "agents.enabled = false\nagents.max_concurrent_threads_per_session = 1");

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
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(
                projectConfigPath,
                "agents.enabled = true\nagents.max_concurrent_threads_per_session = 2");
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
            Assert.Equal(1, submittedPlan.CodexMaximumConcurrentSubagentRuns);
            Assert.Equal(1, submittedRequest.CodexMaximumConcurrentSubagentRuns);
            Assert.False(submittedPlan.RequiresDelegatedWorkspaceEvidence);
            Assert.True(refreshed.ConfiguredAgentsEnabled);
            Assert.Equal(2, refreshed.ConfiguredMaximumConcurrentSubagentRuns);
            Assert.True(refreshedPlan.CodexAgentsEnabled);
            Assert.Equal(2, refreshedPlan.CodexMaximumConcurrentSubagentRuns);
            Assert.True(refreshedPlan.RequiresDelegatedWorkspaceEvidence);
            Assert.False(queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredAgentsEnabled);
            Assert.Equal(1, queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredMaximumConcurrentSubagentRuns);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submittedContext.ProjectInstructionDiscoveryOptions.MaximumConcurrentSubagentRunsSource);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void MultiAgentFeatureAndAgentsEnabledAreBothRequiredAndFrozen()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [features]
                multi_agent = true

                [agents]
                enabled = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                "[features]\nmulti_agent = false\n\n[agents]\nenabled = true");

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
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(
                projectConfigPath,
                "[features]\nmulti_agent = true\n\n[agents]\nenabled = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredMultiAgentEnabled);
            Assert.True(submitted.ConfiguredAgentsEnabled);
            Assert.False(submitted.EffectiveAgentsEnabled);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.MultiAgentEnabledSource);
            Assert.False(submittedPlan.CodexAgentsEnabled);
            Assert.False(submittedRequest.CodexAgentsEnabled);
            Assert.False(submittedPlan.RequiresDelegatedWorkspaceEvidence);
            Assert.True(refreshed.ConfiguredMultiAgentEnabled);
            Assert.True(refreshed.EffectiveAgentsEnabled);
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
                [features]
                multi_agent = false

                [agents]
                enabled = false
                max_concurrent_threads_per_session = 1

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[features]\nmulti_agent = true\n\n[agents]\nenabled = true\nmax_threads = 3");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredMultiAgentEnabled);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.MultiAgentEnabledSource);
            Assert.False(untrusted.ConfiguredAgentsEnabled);
            Assert.Equal(1, untrusted.ConfiguredMaximumConcurrentSubagentRuns);
            Assert.True(untrusted.HasAgentsEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.AgentsEnabledSource);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.MaximumConcurrentSubagentRunsSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[features]\nmulti_agent = \"false\"\n\n[agents]\nenabled = \"false\"\nmax_concurrent_threads_per_session = 0\nmax_threads = -1");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(invalid.HasMultiAgentEnabledOverride);
            Assert.True(invalid.ConfiguredMultiAgentEnabled);
            Assert.False(invalid.HasAgentsEnabledOverride);
            Assert.False(invalid.HasMaximumConcurrentSubagentRunsOverride);
            Assert.True(invalid.ConfiguredAgentsEnabled);
            Assert.Equal(
                CopilotSubagentCoordinator.DefaultMaximumConcurrentRuns,
                invalid.ConfiguredMaximumConcurrentSubagentRuns);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CanonicalConcurrencyKeyWinsOverTheLegacyAlias()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[agents]\nmax_concurrent_threads_per_session = 3\nmax_threads = 4");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(options.HasMaximumConcurrentSubagentRunsOverride);
            Assert.Equal(3, options.ConfiguredMaximumConcurrentSubagentRuns);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ConfiguredConcurrencyLimitQueuesTheNextSubagentLease()
    {
        var coordinator = new CopilotSubagentCoordinator(new CopilotAgentRequest
        {
            CodexMaximumConcurrentSubagentRuns = 1,
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var first = await coordinator.TryAcquireAsync("explore", cancellation.Token);
        Assert.NotNull(first);

        var secondTask = coordinator.TryAcquireAsync("scout", cancellation.Token);
        Assert.False(secondTask.IsCompleted);

        first.Dispose();
        using var second = await secondTask;
        Assert.NotNull(second);
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
            ConfiguredMultiAgentEnabled = false,
            HasMultiAgentEnabledOverride = true,
            MultiAgentEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredAgentsEnabled = true,
            HasAgentsEnabledOverride = true,
            AgentsEnabledSource = CopilotProjectInstructionConfigSources.TrustedProject,
            ConfiguredMaximumConcurrentSubagentRuns = 1,
            HasMaximumConcurrentSubagentRunsOverride = true,
            MaximumConcurrentSubagentRunsSource = CopilotProjectInstructionConfigSources.TrustedProject,
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
            CodexMultiAgentEnabled = false,
            HasCodexMultiAgentEnabledOverride = true,
            CodexMultiAgentEnabledSourceLabel = options.MultiAgentEnabledSourceLabel,
            CodexAgentsEnabled = true,
            HasCodexAgentsEnabledOverride = true,
            CodexAgentsEnabledSourceLabel = options.AgentsEnabledSourceLabel,
            CodexMaximumConcurrentSubagentRuns = 1,
            HasCodexMaximumConcurrentSubagentRunsOverride = true,
            CodexMaximumConcurrentSubagentRunsSourceLabel = options.MaximumConcurrentSubagentRunsSourceLabel,
        });

        Assert.Contains("Codex features.multi_agent：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.MultiAgentEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.enabled：true", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.AgentsEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.max_concurrent_threads_per_session：1", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.MaximumConcurrentSubagentRunsSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("拒绝旧计划", memoryReport, StringComparison.Ordinal);
        Assert.Contains("V1 多代理功能：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("Agents 配置：开启", contextReport, StringComparison.Ordinal);
        Assert.Contains("子代理工具（有效）：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("子代理并发槽位：1", contextReport, StringComparison.Ordinal);
        Assert.Contains("旧调用也会拒绝", contextReport, StringComparison.Ordinal);
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
