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

        Assert.Contains(disabledSurface.Entries, entry => entry.SourceId == "builtin:write-tool-policy");
        Assert.DoesNotContain(disabledSurface.Entries, entry => entry.SourceId.StartsWith("extension:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(enabledSurface.Entries, entry => entry.SourceId == "extension:test:hook:snapshot");
        Assert.NotEqual(disabledSurface.Fingerprint, enabledSurface.Fingerprint);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, disabledCompatibility.Kind);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.HookSurfaceDrift, enabledCompatibility.Kind);
        Assert.Contains("Codex features.hooks：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.HooksEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("内置写入安全策略仍保留", memoryReport, StringComparison.Ordinal);
        Assert.Contains("模块扩展 Hook：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.hooks：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("checkpoint 按有效 Hook 面校验", debugReport, StringComparison.Ordinal);
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
