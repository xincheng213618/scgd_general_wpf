using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexPreventIdleSleepTests
{
    [Fact]
    public void UntrustedAndInvalidFeatureValuesCannotReplaceTheCodexHomeContract()
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
                prevent_idle_sleep = true

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[features]\nprevent_idle_sleep = false");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.True(untrusted.ConfiguredPreventIdleSleep);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.PreventIdleSleepSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[features]\nprevent_idle_sleep = \"true\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(invalid.HasPreventIdleSleepOverride);
            Assert.False(invalid.ConfiguredPreventIdleSleep);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ActiveTurnPolicyAcquiresOnlyForAnExplicitTrueSnapshotAndAlwaysReleases()
    {
        var factory = new RecordingSleepRequestFactory();
        var enabled = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredPreventIdleSleep = true,
            HasPreventIdleSleepOverride = true,
        };
        var disabled = enabled with
        {
            ConfiguredPreventIdleSleep = false,
        };
        var unconfigured = enabled with
        {
            HasPreventIdleSleepOverride = false,
        };

        using (CopilotActiveTurnSleepPrevention.Acquire(disabled, factory))
        {
            Assert.Equal(0, factory.AcquireCount);
        }
        using (CopilotActiveTurnSleepPrevention.Acquire(unconfigured, factory))
        {
            Assert.Equal(0, factory.AcquireCount);
        }
        using (CopilotActiveTurnSleepPrevention.Acquire(enabled, factory))
        {
            Assert.Equal(1, factory.AcquireCount);
            Assert.Equal(0, factory.DisposeCount);
        }

        Assert.Equal(1, factory.DisposeCount);
    }

    [Fact]
    public void PreventIdleSleepDiagnosticsExposeValueSourceLifecycleAndRuntimeState()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredPreventIdleSleep = true,
            HasPreventIdleSleepOverride = true,
            PreventIdleSleepSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexPreventIdleSleep = true,
            HasCodexPreventIdleSleepOverride = true,
            CodexPreventIdleSleepSourceLabel = options.PreventIdleSleepSourceLabel,
            ActiveSleepPreventionLeaseCount = 1,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex features.prevent_idle_sleep：true", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.PreventIdleSleepSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("仅活动轮次", memoryReport, StringComparison.Ordinal);
        Assert.Contains("排队等待不占用", memoryReport, StringComparison.Ordinal);
        Assert.Contains("活动轮次防休眠：开启", contextReport, StringComparison.Ordinal);
        Assert.Contains("Windows Power Request 活动 1 个", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.prevent_idle_sleep：true", debugReport, StringComparison.Ordinal);
        Assert.Contains("提交快照", debugReport, StringComparison.Ordinal);
    }

    private sealed class RecordingSleepRequestFactory : ICopilotSystemSleepRequestFactory
    {
        public int AcquireCount { get; private set; }

        public int DisposeCount { get; private set; }

        public IDisposable Acquire()
        {
            AcquireCount++;
            return new CallbackDisposable(() => DisposeCount++);
        }
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private Action? _callback;

        public CallbackDisposable(Action callback)
        {
            _callback = callback;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _callback, null)?.Invoke();
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-prevent-sleep-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
