using ColorVision.Copilot;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentExtensionHookBridgeTests
{
    [Fact]
    public async Task ExtensionHooksArePublishedAndRemovedAsOneRegistryRevision()
    {
        var extensionRegistry = new CopilotAgentExtensionRegistry();
        var hookRegistry = new CopilotToolExecutionHookRegistry();
        using var bridge = new CopilotAgentExtensionBridge(
            extensionRegistry,
            new CopilotCapabilityCatalog(),
            reservedToolNames: [],
            hookRegistry);
        var sequence = new List<string>();
        var firstHook = new RecordingModuleHook("First", "^ExtensionProbe$", 10, sequence);
        var secondHook = new RecordingModuleHook("Second", "^ExtensionProbe$", 20, sequence);
        var extensionRegistration = extensionRegistry.Register(CreateRegistration(firstHook, secondHook));

        var published = hookRegistry.GetSnapshot();
        var bridgeSnapshot = bridge.GetSnapshot();
        Assert.Equal(1, published.Revision);
        Assert.Equal(
            ["extension:test.extension:hook:first", "extension:test.extension:hook:second"],
            published.Entries.Select(entry => entry.SourceId));
        var source = Assert.Single(bridgeSnapshot.Sources);
        Assert.Equal(2, source.DeclaredHookCount);
        Assert.Equal(2, source.ActiveHookCount);
        Assert.All(source.Hooks, hook => Assert.True(hook.IsActive));

        var tool = new RecordingTool("ExtensionProbe", sequence);
        var outcome = await new CopilotToolExecutor(hookRegistry).ExecuteAsync(
            CreateInvocation(tool, "extension-hooks"),
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal(
            ["before:First", "before:Second", "tool:ExtensionProbe", "after:First", "after:Second"],
            sequence);
        Assert.Equal(
            [
                "BeforeExecute:builtin:write-tool-policy:Completed",
                "BeforeExecute:extension:test.extension:hook:first:Completed",
                "BeforeExecute:extension:test.extension:hook:second:Completed",
                "AfterExecute:builtin:write-tool-policy:Completed",
                "AfterExecute:extension:test.extension:hook:first:Completed",
                "AfterExecute:extension:test.extension:hook:second:Completed",
            ],
            outcome.HookRuns.Select(run => $"{run.Phase}:{run.SourceId}:{run.State}"));
        Assert.Equal("extension-hooks", firstHook.BeforeContext?.CallId);
        Assert.Equal("value", firstHook.BeforeContext?.Arguments["query"]);
        Assert.Equal(CopilotModuleToolExecutionState.Completed, firstHook.AfterOutcome?.State);

        extensionRegistration.Dispose();

        var removed = hookRegistry.GetSnapshot();
        Assert.Equal(2, removed.Revision);
        Assert.Empty(removed.Entries);
        Assert.Empty(bridge.GetSnapshot().Sources);
        await new CopilotToolExecutor(hookRegistry).ExecuteAsync(
            CreateInvocation(tool, "after-unload"),
            _ => { },
            CancellationToken.None);
        Assert.Equal(1, firstHook.BeforeCount);
        Assert.Equal(1, secondHook.BeforeCount);
    }

    [Fact]
    public async Task ExtensionHookDenialBlocksToolAndKeepsStableSourceEvidence()
    {
        var extensionRegistry = new CopilotAgentExtensionRegistry();
        var hookRegistry = new CopilotToolExecutionHookRegistry();
        using var bridge = new CopilotAgentExtensionBridge(
            extensionRegistry,
            new CopilotCapabilityCatalog(),
            reservedToolNames: [],
            hookRegistry);
        var hook = new RecordingModuleHook(
            "Policy",
            "^DeniedProbe$",
            0,
            [],
            _ => CopilotModuleToolExecutionHookDecision.Deny(
                "The module policy denied this call.",
                "Module Policy Denied"));
        using var extensionRegistration = extensionRegistry.Register(CreateRegistration(hook));
        var tool = new RecordingTool("DeniedProbe", []);
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor(hookRegistry).ExecuteAsync(
            CreateInvocation(tool, "extension-denial"),
            events.Add,
            CancellationToken.None);

        Assert.False(outcome.Result.Success);
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Equal("module_policy_denied", outcome.Result.FailureCode);
        Assert.Equal(0, tool.ExecutionCount);
        var deniedRun = Assert.Single(outcome.HookRuns, run =>
            run.SourceId == "extension:test.extension:hook:policy"
            && run.Phase == CopilotToolExecutionHookPhase.BeforeExecute);
        Assert.Equal(CopilotToolExecutionHookState.Denied, deniedRun.State);
        Assert.Equal("module_policy_denied", deniedRun.FailureCode);
        var terminalEvent = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Contains(terminalEvent.ToolExecutionHookRuns, run =>
            run.SourceId == deniedRun.SourceId
            && run.State == CopilotToolExecutionHookState.Denied
            && run.FailureCode == deniedRun.FailureCode);
    }

    [Fact]
    public async Task UnloadedInflightExtensionHookIsSkippedAfterToolExecution()
    {
        var extensionRegistry = new CopilotAgentExtensionRegistry();
        var hookRegistry = new CopilotToolExecutionHookRegistry();
        using var bridge = new CopilotAgentExtensionBridge(
            extensionRegistry,
            new CopilotCapabilityCatalog(),
            reservedToolNames: [],
            hookRegistry);
        IDisposable? extensionRegistration = null;
        var hook = new RecordingModuleHook(
            "SelfUnload",
            "^UnloadProbe$",
            0,
            [],
            _ =>
            {
                extensionRegistration!.Dispose();
                return CopilotModuleToolExecutionHookDecision.Proceed;
            });
        extensionRegistration = extensionRegistry.Register(CreateRegistration(hook));
        var tool = new RecordingTool("UnloadProbe", []);

        var outcome = await new CopilotToolExecutor(hookRegistry).ExecuteAsync(
            CreateInvocation(tool, "inflight-unload"),
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(1, hook.BeforeCount);
        Assert.Equal(0, hook.AfterCount);
        Assert.Contains(outcome.HookRuns, run =>
            run.SourceId == "extension:test.extension:hook:selfunload"
            && run.Phase == CopilotToolExecutionHookPhase.BeforeExecute
            && run.State == CopilotToolExecutionHookState.Completed);
        var skippedRun = Assert.Single(outcome.HookRuns, run =>
            run.SourceId == "extension:test.extension:hook:selfunload"
            && run.Phase == CopilotToolExecutionHookPhase.AfterExecute);
        Assert.Equal(CopilotToolExecutionHookState.Skipped, skippedRun.State);
        Assert.Equal("extension_hook_unloaded", skippedRun.FailureCode);
        Assert.Equal(2, hookRegistry.GetSnapshot().Revision);
        Assert.Empty(hookRegistry.GetSnapshot().Entries);
    }

    [Fact]
    public void ExtensionRegistryRejectsDuplicateAndBacktrackingHookDefinitions()
    {
        var extensionRegistry = new CopilotAgentExtensionRegistry();

        Assert.Throws<ArgumentException>(() => extensionRegistry.Register(CreateRegistration(
            new RecordingModuleHook("Same", "*", 0, []),
            new RecordingModuleHook("same", "*", 1, []))));
        Assert.Throws<ArgumentException>(() => extensionRegistry.Register(CreateRegistration(
            new RecordingModuleHook("Unsafe", "^(a+)\\1$", 0, []))));
    }

    private static CopilotAgentExtensionRegistration CreateRegistration(
        params ICopilotModuleToolExecutionHook[] hooks)
    {
        return new CopilotAgentExtensionRegistration
        {
            SourceId = "test.extension",
            SourceName = "Test extension",
            SourceVersion = "1.0.0",
            ToolExecutionHooks = hooks,
        };
    }

    private static CopilotToolInvocation CreateInvocation(ICopilotTool tool, string callId)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            RuntimeName = "extension-hook-test",
            Tool = tool,
            ToolInput = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = "value",
                },
            },
            AgentRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Diagnose,
                UserText = "Run the extension hook test.",
            },
        };
    }

    private sealed class RecordingModuleHook(
        string name,
        string pattern,
        int order,
        List<string> sequence,
        Func<CopilotModuleToolExecutionHookContext, CopilotModuleToolExecutionHookDecision>? before = null)
        : ICopilotModuleToolExecutionHook
    {
        public string Name => name;

        public string ToolNamePattern => pattern;

        public int Order => order;

        public int BeforeCount { get; private set; }

        public int AfterCount { get; private set; }

        public CopilotModuleToolExecutionHookContext? BeforeContext { get; private set; }

        public CopilotModuleToolExecutionHookOutcome? AfterOutcome { get; private set; }

        public Task<CopilotModuleToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotModuleToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeforeCount++;
            BeforeContext = context;
            sequence.Add("before:" + name);
            return Task.FromResult(before?.Invoke(context) ?? CopilotModuleToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotModuleToolExecutionHookOutcome outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AfterCount++;
            AfterOutcome = outcome;
            sequence.Add("after:" + name);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTool(string name, List<string> sequence) : ICopilotTool
    {
        public string Name => name;

        public string Description => "Records whether the extension probe executed.";

        public CopilotToolCapabilityDescriptor Capability => CopilotToolCapabilityDescriptor.ReadOnly();

        public int ExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            sequence.Add("tool:" + name);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = name,
                Success = true,
                Summary = "Extension probe completed.",
            });
        }
    }
}
