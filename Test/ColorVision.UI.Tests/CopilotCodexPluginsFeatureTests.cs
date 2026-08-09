using ColorVision.Copilot;
using ColorVision.UI;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexPluginsFeatureTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenAcrossContextPlanAndRequest()
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
                plugins = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "features.plugins = false");

            var submittedContext = CreateHostContext(globalRoot, projectRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the active ColorVision extension context.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });

            File.WriteAllText(projectConfigPath, "features.plugins = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var options = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.False(options.ConfiguredPluginsEnabled);
            Assert.True(options.HasPluginsEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                options.PluginsEnabledSource);
            Assert.False(submittedPlan.ContextRequest.IncludeExtensionProviders);
            Assert.False(submittedPlan.CodexPluginsEnabled);
            Assert.False(submittedRequest.CodexPluginsEnabled);
            Assert.True(refreshed.ConfiguredPluginsEnabled);
            Assert.False(submittedPlan.CodexPluginsEnabled);
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
                plugins = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[features]" + Environment.NewLine + "plugins = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredPluginsEnabled);
            Assert.True(untrusted.HasPluginsEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.PluginsEnabledSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[features]" + Environment.NewLine + "plugins = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(invalid.ConfiguredPluginsEnabled);
            Assert.False(invalid.HasPluginsEnabledOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DisabledSnapshotExcludesExtensionContextToolsHooksAndCapabilityDrift()
    {
        var extensionRegistry = new CopilotAgentExtensionRegistry();
        var capabilityCatalog = new CopilotCapabilityCatalog();
        var hookRegistry = new CopilotToolExecutionHookRegistry();
        var builtInTool = new RecordingTool("PluginFeatureBuiltInTool");
        var builtInProvider = new RecordingContextProvider("builtin-context");
        var extensionProvider = new RecordingContextProvider("extension-context");
        var moduleTool = new RecordingModuleTool();
        var moduleHook = new RecordingModuleHook();
        capabilityCatalog.PublishSource(
            CopilotCapabilitySourceKind.BuiltIn,
            "builtin",
            "ColorVision",
            [builtInTool]);
        using var bridge = new CopilotAgentExtensionBridge(
            extensionRegistry,
            capabilityCatalog,
            reservedToolNames: [builtInTool.Name],
            hookRegistry);
        using var registration = extensionRegistry.Register(
            new CopilotAgentExtensionRegistration
            {
                SourceId = "test.plugins-feature",
                SourceName = "Plugins feature extension",
                SourceVersion = "1.0.0",
                ContextProviders = [extensionProvider],
                Tools = [moduleTool],
                ToolExecutionHooks = [moduleHook],
            });
        var toolRegistry = new CopilotToolRegistry([builtInTool], bridge);
        var contextRegistry = new CopilotContextRegistry([builtInProvider], bridge);
        var disabledRequest = CreateAgentRequest(pluginsEnabled: false);
        var enabledRequest = CreateAgentRequest(pluginsEnabled: true);

        var disabledTools = toolRegistry.FindTools(disabledRequest);
        var enabledTools = toolRegistry.FindTools(enabledRequest);
        var disabledContext = await contextRegistry.CaptureAsync(
            CreateContextRequest(includeExtensionProviders: false),
            CancellationToken.None);
        var enabledContext = await contextRegistry.CaptureAsync(
            CreateContextRequest(includeExtensionProviders: true),
            CancellationToken.None);

        Assert.Equal([builtInTool.Name], disabledTools.Select(tool => tool.Name));
        Assert.Contains(enabledTools, tool => tool.Name == builtInTool.Name);
        var extensionTool = Assert.Single(enabledTools, tool => tool.Name == moduleTool.Name);
        Assert.Equal(["builtin-context"], disabledContext.Select(item => item.Id));
        Assert.Equal(
            ["builtin-context", "extension-context"],
            enabledContext.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(2, builtInProvider.CaptureCount);
        Assert.Equal(1, extensionProvider.CaptureCount);

        var deniedDirectCall = await extensionTool.ExecuteAsync(
            disabledRequest,
            CopilotAgentToolInput.Empty,
            CancellationToken.None);
        var enabledDirectCall = await extensionTool.ExecuteAsync(
            enabledRequest,
            CopilotAgentToolInput.Empty,
            CancellationToken.None);

        Assert.False(deniedDirectCall.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, deniedDirectCall.FailureKind);
        Assert.Contains("features.plugins=false", deniedDirectCall.ErrorMessage, StringComparison.Ordinal);
        Assert.True(enabledDirectCall.Success);
        Assert.Equal(1, moduleTool.ExecutionCount);

        var executor = new CopilotToolExecutor(hookRegistry);
        var disabledExecution = await executor.ExecuteAsync(
            CreateInvocation(builtInTool, disabledRequest, "plugins-disabled-hook"),
            _ => { },
            CancellationToken.None);
        var enabledExecution = await executor.ExecuteAsync(
            CreateInvocation(builtInTool, enabledRequest, "plugins-enabled-hook"),
            _ => { },
            CancellationToken.None);

        Assert.True(disabledExecution.Result.Success);
        Assert.True(enabledExecution.Result.Success);
        Assert.DoesNotContain(
            disabledExecution.HookRuns,
            run => run.SourceId.StartsWith("extension:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            enabledExecution.HookRuns,
            run => run.SourceId == "extension:test.plugins-feature:hook:plugins_feature_hook");
        Assert.Equal(1, moduleHook.BeforeCount);
        Assert.Equal(1, moduleHook.AfterCount);

        var fullSnapshot = capabilityCatalog.GetSnapshot();
        var filteredSnapshot = capabilityCatalog.GetSnapshot(includePluginCapabilities: false);
        Assert.Contains(fullSnapshot.Capabilities, entry => entry.SourceKind == CopilotCapabilitySourceKind.Plugin);
        Assert.DoesNotContain(filteredSnapshot.Capabilities, entry => entry.SourceKind == CopilotCapabilitySourceKind.Plugin);
        Assert.Equal([builtInTool.Name], filteredSnapshot.Capabilities.Select(entry => entry.Name));
        var profile = CopilotProfileConfig.CreateDefault();
        var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
            CopilotAgentSessionCheckpoint.Create(
                profile,
                "{}",
                filteredSnapshot,
                availableToolNames: [builtInTool.Name]));
        capabilityCatalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "plugin:hot-update",
            "Hot plugin",
            [new RecordingTool("PluginFeatureHotTool")]);
        var afterPluginUpdate = capabilityCatalog.GetSnapshot(includePluginCapabilities: false);
        var compatibility = checkpoint.EvaluateFor(
            profile,
            afterPluginUpdate,
            availableToolNames: [builtInTool.Name]);

        Assert.Equal(filteredSnapshot.Revision, afterPluginUpdate.Revision);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, compatibility.Kind);
    }

    [Fact]
    public void ModuleAsyncHookModeFlowsThroughBridgeAndRuntimeSnapshot()
    {
        var extensionRegistry = new CopilotAgentExtensionRegistry();
        var hookRegistry = new CopilotToolExecutionHookRegistry();
        using var bridge = new CopilotAgentExtensionBridge(
            extensionRegistry,
            new CopilotCapabilityCatalog(),
            reservedToolNames: [],
            hookRegistry);
        using var registration = extensionRegistry.Register(
            new CopilotAgentExtensionRegistration
            {
                SourceId = "test.plugins-async-hook",
                SourceName = "Async hook extension",
                SourceVersion = "1.0.0",
                ToolExecutionHooks = [new AsyncModuleHook()],
            });

        var source = Assert.Single(bridge.GetSnapshot().Sources);
        var declaredHook = Assert.Single(source.Hooks);
        Assert.True(declaredHook.IsActive);
        Assert.Equal(CopilotToolExecutionHookMode.Async, declaredHook.ExecutionMode);
        var runtimeHook = Assert.Single(hookRegistry.GetSnapshot().Entries);
        Assert.Equal(declaredHook.SourceId, runtimeHook.SourceId);
        Assert.Equal(CopilotToolExecutionHookMode.Async, runtimeHook.ExecutionMode);
    }

    [Fact]
    public void DiagnosticsAndHarnessExposeTheFrozenPluginBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredPluginsEnabled = false,
            HasPluginsEnabledOverride = true,
            PluginsEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexHooksEnabled = true,
            CodexPluginsEnabled = false,
            HasCodexPluginsEnabledOverride = true,
            CodexPluginsEnabledSourceLabel = options.PluginsEnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });
        string harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            CreateAgentRequest(pluginsEnabled: false),
            [new RecordingTool("PluginFeatureHarnessTool")],
            new CopilotAgentEnvironmentContext(),
            taskLedgerEnabled: false,
            agentModeEnabled: true);

        Assert.Contains("Codex features.plugins：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains("不卸载主程序业务插件", memoryReport, StringComparison.Ordinal);
        Assert.Contains("features.plugins=false", contextReport, StringComparison.Ordinal);
        Assert.Contains("内置工具、外部 MCP", contextReport, StringComparison.Ordinal);
        Assert.Contains("features.plugins：false", debugReport, StringComparison.Ordinal);
        Assert.Contains(options.PluginsEnabledSourceLabel, debugReport, StringComparison.Ordinal);
        Assert.Contains("features.plugins=false is frozen", harness, StringComparison.Ordinal);
        Assert.Contains("independently configured external MCP tools are unaffected", harness, StringComparison.Ordinal);
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

    private static CopilotAgentRequest CreateAgentRequest(bool pluginsEnabled) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        ConversationId = "plugins-feature-conversation",
        TaskId = "plugins-feature-task",
        UserText = "Inspect and run the plugins feature test.",
        TaskIntentText = "Inspect and run the plugins feature test.",
        Mode = CopilotAgentMode.Code,
        CodexHooksEnabled = true,
        CodexPluginsEnabled = pluginsEnabled,
    };

    private static CopilotContextRequest CreateContextRequest(bool includeExtensionProviders) => new()
    {
        Scope = CopilotContextScope.Agent,
        UserText = "Inspect the plugins feature context.",
        IncludeExtensionProviders = includeExtensionProviders,
    };

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        CopilotAgentRequest request,
        string callId) => new()
    {
        CallId = callId,
        RuntimeName = "plugins-feature-test",
        Tool = tool,
        AgentRequest = request,
        ToolInput = CopilotAgentToolInput.Empty,
    };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-codex-plugins-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingContextProvider(string id) : ICopilotContextProvider
    {
        private int _captureCount;

        public int Order => 0;

        public int CaptureCount => Volatile.Read(ref _captureCount);

        public bool CanProvide(CopilotContextScope scope) => true;

        public Task<CopilotContextItem?> CaptureAsync(
            CopilotContextRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _captureCount);
            return Task.FromResult<CopilotContextItem?>(new CopilotContextItem
            {
                Id = id,
                Title = id,
                Summary = id,
                Content = id,
            });
        }
    }

    private sealed class RecordingModuleTool : ICopilotModuleTool
    {
        private int _executionCount;

        public string Name => "PluginFeatureModuleTool";

        public string Description => "A read-only module tool used to verify the Codex plugins feature gate.";

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public Task<CopilotModuleToolResult> ExecuteAsync(
            CopilotModuleToolRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(CopilotModuleToolResult.Ok("Module tool executed."));
        }
    }

    private sealed class RecordingModuleHook : ICopilotModuleToolExecutionHook
    {
        private int _beforeCount;
        private int _afterCount;

        public string Name => "Plugins_Feature_Hook";

        public string ToolNamePattern => "^PluginFeatureBuiltInTool$";

        public int BeforeCount => Volatile.Read(ref _beforeCount);

        public int AfterCount => Volatile.Read(ref _afterCount);

        public Task<CopilotModuleToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotModuleToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _beforeCount);
            return Task.FromResult(CopilotModuleToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotModuleToolExecutionHookOutcome outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _afterCount);
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncModuleHook : ICopilotModuleToolExecutionHook
    {
        public string Name => "Async_Module_Hook";

        public CopilotModuleToolExecutionHookMode ExecutionMode =>
            CopilotModuleToolExecutionHookMode.Async;

        public Task<CopilotModuleToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotModuleToolExecutionHookContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(CopilotModuleToolExecutionHookDecision.Proceed);
    }

    private sealed class RecordingTool(string name) : ICopilotTool
    {
        private int _executionCount;

        public string Name { get; } = name;

        public string Description => "A read-only tool used to verify the Codex plugins feature gate.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly();

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
                Summary = "Recording tool executed.",
            });
        }
    }
}
