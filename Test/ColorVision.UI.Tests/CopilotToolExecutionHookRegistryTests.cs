using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolExecutionHookRegistryTests
{
    [Fact]
    public async Task RegistryMatchesToolsAndRunsHooksInDeterministicOrder()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        var sequence = new List<string>();
        using var late = registry.Register(
            "test:late",
            new SequenceHook("late", sequence),
            "^RegistryProbe$",
            order: 20);
        using var ignored = registry.Register(
            "test:ignored",
            new SequenceHook("ignored", sequence),
            "^DifferentTool$",
            order: 0);
        using var early = registry.Register(
            "test:early",
            new SequenceHook("early", sequence),
            "^RegistryProbe$",
            order: 10);
        var tool = new SequenceTool("RegistryProbe", sequence);

        var outcome = await new CopilotToolExecutor(registry).ExecuteAsync(
            CreateInvocation(tool, "registry-order"),
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal(
            ["before:early", "before:late", "tool:RegistryProbe", "after:early", "after:late"],
            sequence);
        Assert.Equal(
            [
                "BeforeExecute:builtin:write-tool-policy:Completed",
                "BeforeExecute:test:early:Completed",
                "BeforeExecute:test:late:Completed",
                "AfterExecute:builtin:write-tool-policy:Completed",
                "AfterExecute:test:early:Completed",
                "AfterExecute:test:late:Completed",
            ],
            outcome.HookRuns.Select(item => $"{item.Phase}:{item.SourceId}:{item.State}"));
        var snapshot = registry.GetSnapshot();
        Assert.Equal(3, snapshot.Revision);
        Assert.Equal(64, snapshot.Fingerprint.Length);
        Assert.All(snapshot.Entries, entry => Assert.Equal(64, entry.DefinitionFingerprint.Length));
        Assert.Equal(["test:ignored", "test:early", "test:late"], snapshot.Entries.Select(item => item.SourceId));
    }

    [Fact]
    public async Task RegistrationChangesAffectFutureCallsWithoutSplittingAnInflightLifecycle()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        var sequence = new List<string>();
        IDisposable? registration = null;
        var hook = new SequenceHook(
            "self-removing",
            sequence,
            before: () => registration!.Dispose());
        registration = registry.Register(
            "test:self-removing",
            hook,
            "^SnapshotProbe$");
        var executor = new CopilotToolExecutor(registry);
        var tool = new SequenceTool("SnapshotProbe", sequence);

        await executor.ExecuteAsync(
            CreateInvocation(tool, "snapshot-first"),
            _ => { },
            CancellationToken.None);
        await executor.ExecuteAsync(
            CreateInvocation(tool, "snapshot-second"),
            _ => { },
            CancellationToken.None);

        Assert.Equal(
            ["before:self-removing", "tool:SnapshotProbe", "after:self-removing", "tool:SnapshotProbe"],
            sequence);
        var snapshot = registry.GetSnapshot();
        Assert.Equal(2, snapshot.Revision);
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public async Task DefaultExecutorResolvesTheSharedProductionRegistry()
    {
        var sequence = new List<string>();
        using var registration = CopilotToolExecutionHookRegistry.Shared.Register(
            "test:shared-production-probe",
            new SequenceHook("shared", sequence),
            "^SharedRegistryProbe$");
        var tool = new SequenceTool("SharedRegistryProbe", sequence);

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(tool, "shared-registry"),
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal(
            ["before:shared", "tool:SharedRegistryProbe", "after:shared"],
            sequence);
        var hookSurface = new CopilotToolExecutor().GetHookSurfaceSnapshot();
        Assert.Equal("builtin:write-tool-policy", hookSurface.Entries[0].SourceId);
        Assert.Contains(hookSurface.Entries, entry => entry.SourceId == "test:shared-production-probe");
        Assert.Equal(64, hookSurface.Fingerprint.Length);
    }

    [Fact]
    public void SnapshotFingerprintTracksEffectiveDefinitionButNotRegistryRevision()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        var hook = new SequenceHook("stable", []);
        var firstRegistration = registry.Register(
            "test:definition",
            hook,
            "^FingerprintProbe$",
            order: 10);
        var first = registry.GetSnapshot();
        firstRegistration.Dispose();
        var secondRegistration = registry.Register(
            "test:definition",
            hook,
            "^FingerprintProbe$",
            order: 10);
        var second = registry.GetSnapshot();
        secondRegistration.Dispose();
        using var configuredRegistration = registry.Register(
            "test:definition",
            hook,
            "^FingerprintProbe$",
            order: 10,
            configurationFingerprint: new string('a', 64));
        var configured = registry.GetSnapshot();
        var tamperedFingerprint = (first.Fingerprint[0] == '0' ? "1" : "0") + first.Fingerprint[1..];
        var tampered = new CopilotToolExecutionHookRegistrySnapshot
        {
            Revision = first.Revision,
            Fingerprint = tamperedFingerprint,
            Entries = first.Entries,
        };

        Assert.True(first.IsStructurallyValid());
        Assert.NotEqual(first.Revision, second.Revision);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.NotEqual(second.Fingerprint, configured.Fingerprint);
        Assert.False(tampered.IsStructurallyValid());
    }

    [Fact]
    public void RegistryRejectsAmbiguousSourcesAndUnsafeMatchers()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        var sequence = new List<string>();
        using var registration = registry.Register(
            "test:unique-source",
            new SequenceHook("first", sequence));

        Assert.Throws<InvalidOperationException>(() => registry.Register(
            "TEST:UNIQUE-SOURCE",
            new SequenceHook("duplicate", sequence)));
        Assert.Throws<ArgumentException>(() => registry.Register(
            "test:backtracking-pattern",
            new SequenceHook("invalid", sequence),
            "^(a+)\\1$"));
        Assert.Throws<ArgumentException>(() => registry.Register(
            "test:invalid-definition",
            new SequenceHook("invalid-definition", sequence),
            configurationFingerprint: "not-a-sha256"));
        Assert.Throws<ArgumentException>(() => registry.Register(
            "builtin:write-tool-policy",
            new SequenceHook("reserved-source", sequence)));
    }

    private static CopilotToolInvocation CreateInvocation(ICopilotTool tool, string callId)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            RuntimeName = "hook-registry-test",
            Tool = tool,
            AgentRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                UserText = "Run the registered hook test.",
            },
        };
    }

    private sealed class SequenceHook(
        string name,
        List<string> sequence,
        Action? before = null) : ICopilotToolExecutionHook
    {
        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sequence.Add("before:" + name);
            before?.Invoke();
            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sequence.Add("after:" + name);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceTool(string name, List<string> sequence) : ICopilotTool
    {
        public string Name => name;

        public string Description => "Records registry execution order.";

        public CopilotToolCapabilityDescriptor Capability => CopilotToolCapabilityDescriptor.ReadOnly();

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sequence.Add("tool:" + name);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = name,
                Success = true,
                Summary = "Registry probe completed.",
            });
        }
    }
}
