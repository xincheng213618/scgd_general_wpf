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
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
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
        Assert.Contains("Codex features.multi_agent：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.enabled：true", debugReport, StringComparison.Ordinal);
        Assert.Contains("子代理工具（有效）：关闭", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.max_concurrent_threads_per_session：1", debugReport, StringComparison.Ordinal);
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
