using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexHooksFeatureTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenIntoTheSubmittedTurn()
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
                hooks = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "features.hooks = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the workspace.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });

            File.WriteAllText(projectConfigPath, "features.hooks = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredHooksEnabled);
            Assert.True(submitted.HasHooksEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.HooksEnabledSource);
            Assert.False(submittedPlan.CodexHooksEnabled);
            Assert.False(submittedRequest.CodexHooksEnabled);
            Assert.True(refreshed.ConfiguredHooksEnabled);
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
                hooks = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[features]\nhooks = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredHooksEnabled);
            Assert.True(untrusted.HasHooksEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.HooksEnabledSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[features]\nhooks = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(invalid.ConfiguredHooksEnabled);
            Assert.False(invalid.HasHooksEnabledOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DisabledSnapshotOmitsExtensionPermissionHooks()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        var hook = new RecordingExtensionHook(denyPermission: true);
        using var registration = registry.Register(
            "extension:test:hook:permission",
            hook,
            "^HooksFeatureTool$");
        var executor = new CopilotToolExecutor(registry);
        var tool = new RecordingTool(writeCapable: true);

        var disabled = await executor.EvaluatePermissionRequestAsync(
            CreateInvocation(tool, "hooks-disabled-permission", codexHooksEnabled: false),
            CancellationToken.None);
        var enabled = await executor.EvaluatePermissionRequestAsync(
            CreateInvocation(tool, "hooks-enabled-permission", codexHooksEnabled: true),
            CancellationToken.None);

        Assert.True(disabled.Decision.ShouldPrompt);
        Assert.Empty(disabled.HookRuns);
        Assert.Equal(
            ["builtin:write-tool-policy"],
            disabled.HookBindings.Select(binding => binding.SourceId));
        Assert.False(enabled.Decision.ShouldPrompt);
        Assert.Equal("extension_permission_denied", enabled.Decision.FailureCode);
        Assert.Equal(1, hook.PermissionCount);
        Assert.Equal(
            "extension:test:hook:permission",
            Assert.Single(enabled.HookRuns).SourceId);
    }

    [Fact]
    public async Task DisabledSnapshotFiltersFrozenExtensionBindingsAndKeepsBuiltinSafetyHook()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        var hook = new RecordingExtensionHook(denyBeforeExecute: true);
        using var registration = registry.Register(
            "extension:test:hook:lifecycle",
            hook,
            "^HooksFeatureTool$");
        var executor = new CopilotToolExecutor(registry);
        var tool = new RecordingTool();
        var enabledInvocation = CreateInvocation(
            tool,
            "hooks-enabled-reservation",
            codexHooksEnabled: true);
        var frozen = await executor.EvaluatePermissionRequestAsync(
            enabledInvocation,
            CancellationToken.None);

        var disabledBaseInvocation = CreateInvocation(
            tool,
            "hooks-disabled-execution",
            codexHooksEnabled: false);
        var disabledInvocation = new CopilotToolInvocation
        {
            CallId = disabledBaseInvocation.CallId,
            RuntimeName = disabledBaseInvocation.RuntimeName,
            Tool = disabledBaseInvocation.Tool,
            AgentRequest = disabledBaseInvocation.AgentRequest,
            InitialHookRuns = frozen.HookRuns,
            InitialHookBindings = frozen.HookBindings,
        };
        var outcome = await executor.ExecuteAsync(
            disabledInvocation,
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(0, hook.BeforeExecuteCount);
        Assert.NotEmpty(outcome.HookRuns);
        Assert.All(
            outcome.HookRuns,
            run => Assert.Equal("builtin:write-tool-policy", run.SourceId));
    }

    [Fact]
    public void EffectiveHookSurfaceDrivesCheckpointCompatibilityAndDiagnostics()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        using var registration = registry.Register(
            "extension:test:hook:snapshot",
            new RecordingExtensionHook(),
            "^HooksFeatureTool$");
        var executor = new CopilotToolExecutor(registry);
        var disabledSurface = executor.GetHookSurfaceSnapshot(codexHooksEnabled: false);
        var enabledSurface = executor.GetHookSurfaceSnapshot(codexHooksEnabled: true);
        var profile = CopilotProfileConfig.CreateDefault();
        var capabilities = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
            CopilotAgentSessionCheckpoint.Create(
                profile,
                "{}",
                capabilities,
                hookSurfaceSnapshot: disabledSurface));
        var disabledCompatibility = checkpoint.EvaluateFor(
            profile,
            capabilities,
            hookSurfaceSnapshot: disabledSurface);
        var enabledCompatibility = checkpoint.EvaluateFor(
            profile,
            capabilities,
            hookSurfaceSnapshot: enabledSurface);
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredHooksEnabled = false,
            HasHooksEnabledOverride = true,
            HooksEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexHooksEnabled = false,
            HasCodexHooksEnabledOverride = true,
            CodexHooksEnabledSourceLabel = options.HooksEnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });
        string hooksReport = CopilotHookDiagnostics.Format(new CopilotHookDiagnosticSnapshot
        {
            HookSurface = enabledSurface,
        });

        Assert.Contains(disabledSurface.Entries, entry => entry.SourceId == "builtin:write-tool-policy");
        Assert.DoesNotContain(disabledSurface.Entries, entry => entry.SourceId.StartsWith("extension:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(enabledSurface.Entries, entry => entry.SourceId == "extension:test:hook:snapshot");
        Assert.All(enabledSurface.Entries, entry => Assert.Equal(
            CopilotToolExecutionHookMode.Sync,
            entry.ExecutionMode));
        Assert.NotEqual(disabledSurface.Fingerprint, enabledSurface.Fingerprint);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, disabledCompatibility.Kind);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.HookSurfaceDrift, enabledCompatibility.Kind);
        Assert.Contains("Codex features.hooks：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.HooksEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("内置写入安全策略仍保留", memoryReport, StringComparison.Ordinal);
        Assert.Contains("模块扩展 Hook：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.hooks：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("checkpoint 按有效 Hook 面校验", debugReport, StringComparison.Ordinal);
        Assert.Contains("mode sync", hooksReport, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncHooksRunAsBoundedNotificationsWithoutControllingTheToolCall()
    {
        const string sourceId = "extension:test:hook:async-notification";
        var registry = new CopilotToolExecutionHookRegistry();
        var hook = new BlockingAsyncHook();
        using var registration = registry.Register(
            sourceId,
            hook,
            "^HooksFeatureTool$",
            executionMode: CopilotToolExecutionHookMode.Async);
        var executor = new CopilotToolExecutor(registry);
        var tool = new RecordingTool();
        var events = new List<CopilotAgentEvent>();

        CopilotToolExecutionOutcome outcome;
        try
        {
            outcome = await executor.ExecuteAsync(
                CreateInvocation(tool, "hooks-async-notification", codexHooksEnabled: true),
                events.Add,
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await hook.WaitForCallbacksStartedAsync();

            Assert.True(outcome.Result.Success);
            Assert.Equal(1, tool.ExecutionCount);
            Assert.False(hook.BeforeCompleted);
            Assert.False(hook.AfterCompleted);
            Assert.DoesNotContain(
                events,
                agentEvent => string.Equals(
                    agentEvent.ToolExecutionHook?.SourceId,
                    sourceId,
                    StringComparison.Ordinal));

            var scheduled = outcome.HookRuns
                .Where(run => run.SourceId == sourceId)
                .ToArray();
            Assert.Equal(2, scheduled.Length);
            Assert.All(scheduled, run =>
            {
                Assert.Equal(CopilotToolExecutionHookMode.Async, run.ExecutionMode);
                Assert.Equal(CopilotToolExecutionHookState.Scheduled, run.State);
                Assert.Empty(run.FailureCode);
            });
            Assert.Equal(
                [CopilotToolExecutionHookPhase.BeforeExecute, CopilotToolExecutionHookPhase.AfterExecute],
                scheduled.Select(run => run.Phase));

            var hookSurface = executor.GetHookSurfaceSnapshot();
            var asyncDefinition = Assert.Single(
                hookSurface.Entries,
                entry => entry.SourceId == sourceId);
            Assert.Equal(CopilotToolExecutionHookMode.Async, asyncDefinition.ExecutionMode);
            var syncRegistry = new CopilotToolExecutionHookRegistry();
            using var syncRegistration = syncRegistry.Register(
                sourceId,
                hook,
                "^HooksFeatureTool$");
            Assert.NotEqual(
                syncRegistry.GetSnapshot().Fingerprint,
                hookSurface.Fingerprint);
            var audit = Assert.Single(
                CopilotToolExecutionAuditLogger.GetRecentEntries(),
                entry => entry.CallId == "hooks-async-notification");
            Assert.Contains("@async=scheduled", audit.HookSummary, StringComparison.Ordinal);
            string hooksReport = CopilotHookDiagnostics.Format(new CopilotHookDiagnosticSnapshot
            {
                HookSurface = hookSurface,
                RecentToolExecutions = [audit],
            });
            Assert.Contains("mode async", hooksReport, StringComparison.Ordinal);
            Assert.Contains("后台已调度 2", hooksReport, StringComparison.Ordinal);
            Assert.Contains("async · scheduled", hooksReport, StringComparison.Ordinal);
        }
        finally
        {
            hook.ReleaseCallbacks();
        }

        await hook.WaitForCallbacksCompletedAsync();
        Assert.True(hook.BeforeCompleted);
        Assert.True(hook.AfterCompleted);
    }

    [Fact]
    public async Task AsyncHookSchedulerRetainsTimedOutWorkAndRejectsPendingOverflow()
    {
        var scheduler = new CopilotToolExecutionHookBackgroundScheduler();
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstWaveStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var completed = 0;

        for (var index = 0; index < CopilotToolExecutionHookBackgroundScheduler.MaxConcurrency; index++)
        {
            Assert.True(scheduler.TrySchedule(
                $"test:async:{index}",
                CopilotToolExecutionHookPhase.AfterExecute,
                "HooksFeatureTool",
                $"async-capacity-{index}",
                TimeSpan.FromMilliseconds(50),
                async _ =>
                {
                    if (Interlocked.Increment(ref started)
                        == CopilotToolExecutionHookBackgroundScheduler.MaxConcurrency)
                    {
                        firstWaveStarted.TrySetResult(true);
                    }
                    await release.Task;
                    if (Interlocked.Increment(ref completed)
                        == CopilotToolExecutionHookBackgroundScheduler.MaxPending)
                    {
                        allCompleted.TrySetResult(true);
                    }
                }));
        }

        await firstWaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);
        for (var index = CopilotToolExecutionHookBackgroundScheduler.MaxConcurrency;
            index < CopilotToolExecutionHookBackgroundScheduler.MaxPending;
            index++)
        {
            Assert.True(scheduler.TrySchedule(
                $"test:async:{index}",
                CopilotToolExecutionHookPhase.AfterExecute,
                "HooksFeatureTool",
                $"async-capacity-{index}",
                TimeSpan.FromSeconds(5),
                async cancellationToken =>
                {
                    await release.Task.WaitAsync(cancellationToken);
                    if (Interlocked.Increment(ref completed)
                        == CopilotToolExecutionHookBackgroundScheduler.MaxPending)
                    {
                        allCompleted.TrySetResult(true);
                    }
                }));
        }

        Assert.False(scheduler.TrySchedule(
            "test:async:overflow",
            CopilotToolExecutionHookPhase.AfterExecute,
            "HooksFeatureTool",
            "async-capacity-overflow",
            TimeSpan.FromSeconds(5),
            _ => Task.CompletedTask));

        release.TrySetResult(true);
        await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(CopilotToolExecutionHookBackgroundScheduler.MaxPending, completed);
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        string callId,
        bool codexHooksEnabled)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            RuntimeName = "codex-hooks-feature-test",
            Tool = tool,
            AgentRequest = new CopilotAgentRequest
            {
                Profile = CopilotProfileConfig.CreateDefault(),
                Mode = CopilotAgentMode.Code,
                UserText = "Run the hooks feature test.",
                TaskIntentText = "Run the hooks feature test.",
                CodexHooksEnabled = codexHooksEnabled,
            },
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-codex-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingExtensionHook(
        bool denyPermission = false,
        bool denyBeforeExecute = false)
        : ICopilotToolPermissionRequestHook
    {
        private int _permissionCount;
        private int _beforeExecuteCount;

        public int PermissionCount => Volatile.Read(ref _permissionCount);

        public int BeforeExecuteCount => Volatile.Read(ref _beforeExecuteCount);

        public Task<CopilotToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _permissionCount);
            return Task.FromResult(denyPermission
                ? CopilotToolPermissionRequestDecision.Deny(
                    "The extension permission hook denied this call.",
                    "extension_permission_denied")
                : CopilotToolPermissionRequestDecision.Prompt);
        }

        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _beforeExecuteCount);
            return Task.FromResult(denyBeforeExecute
                ? CopilotToolExecutionHookDecision.Deny(
                    "The extension lifecycle hook denied this call.",
                    "extension_lifecycle_denied")
                : CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingAsyncHook : ICopilotToolExecutionHook
    {
        private readonly TaskCompletionSource<bool> _beforeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _afterStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseBefore =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseAfter =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _beforeCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _afterCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool BeforeCompleted => _beforeCompleted.Task.IsCompletedSuccessfully;

        public bool AfterCompleted => _afterCompleted.Task.IsCompletedSuccessfully;

        public async Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            _beforeStarted.TrySetResult(true);
            await _releaseBefore.Task.WaitAsync(cancellationToken);
            _beforeCompleted.TrySetResult(true);
            return CopilotToolExecutionHookDecision.Deny(
                "This async decision must not control the tool call.",
                "async_control_decision_ignored");
        }

        public async Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            _afterStarted.TrySetResult(true);
            await _releaseAfter.Task.WaitAsync(cancellationToken);
            _afterCompleted.TrySetResult(true);
        }

        public async Task WaitForCallbacksStartedAsync()
        {
            await Task.WhenAll(_beforeStarted.Task, _afterStarted.Task)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }

        public async Task WaitForCallbacksCompletedAsync()
        {
            await Task.WhenAll(_beforeCompleted.Task, _afterCompleted.Task)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void ReleaseCallbacks()
        {
            _releaseBefore.TrySetResult(true);
            _releaseAfter.TrySetResult(true);
        }
    }

    private sealed class RecordingTool(bool writeCapable = false) : ICopilotTool
    {
        private int _executionCount;

        public string Name => "HooksFeatureTool";

        public string Description => "Records whether the hooks feature test reached execution.";

        public CopilotToolCapabilityDescriptor Capability => writeCapable
            ? CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent)
            : CopilotToolCapabilityDescriptor.ReadOnly();

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Hooks feature tool completed.",
            });
        }
    }
}
