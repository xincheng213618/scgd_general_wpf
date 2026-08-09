using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentEventProtocolTests
{
    [Fact]
    public void ProtocolAcceptsCanonicalScalarEventsAndWhitespaceDeltas()
    {
        CopilotAgentEvent[] events =
        [
            CopilotAgentEvent.Status("Working."),
            CopilotAgentEvent.RuntimeDiagnostic("Runtime ready."),
            CopilotAgentEvent.ReasoningDelta(" "),
            CopilotAgentEvent.AnswerDelta(" "),
            CopilotAgentEvent.AnswerReset(),
            CopilotAgentEvent.Error("Provider failed."),
            CopilotAgentEvent.Completed(),
            CopilotAgentEvent.CheckpointReady(),
        ];

        foreach (var agentEvent in events)
            CopilotAgentEventProtocol.Validate(agentEvent);
    }

    [Fact]
    public void ReducerRejectsUnexpectedPayloadOnPayloadlessEvent()
    {
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.Completed,
            Budget = new CopilotAgentBudgetSnapshot(),
        };

        var exception = Assert.Throws<InvalidOperationException>(() => Observe(agentEvent));

        Assert.Contains("invalid payload shape", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsMissingRequiredPayload()
    {
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.BudgetUpdated,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => Observe(agentEvent));

        Assert.Contains("invalid payload shape", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsEmptyStreamDelta()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(CopilotAgentEvent.AnswerDelta(string.Empty)));

        Assert.Contains("invalid payload shape", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsToolTextThatDisagreesWithStructuredPayload()
    {
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.ToolStarted,
            Text = "DifferentTool",
            ToolExecution = CreateExecution(),
        };

        var exception = Assert.Throws<InvalidOperationException>(() => Observe(agentEvent));

        Assert.Contains("did not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsUnnormalizedSteeringMessage()
    {
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.SteeringDelivered,
            Text = "Agent provider acknowledged 1 queued user steering instruction(s).",
            SteeringMessages =
            [
                new CopilotSteeringMessageSnapshot(" message:1 ", " continue "),
            ],
        };

        var exception = Assert.Throws<InvalidOperationException>(() => Observe(agentEvent));

        Assert.Contains("unnormalized message", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsDuplicateSteeringMessageIdentity()
    {
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.SteeringRecovery,
            Text = "Agent stopped before delivering 2 queued user steering instruction(s); the input was returned to the conversation draft.",
            SteeringMessages =
            [
                new CopilotSteeringMessageSnapshot("message:1", "first"),
                new CopilotSteeringMessageSnapshot("message:1", "second"),
            ],
        };

        var exception = Assert.Throws<InvalidOperationException>(() => Observe(agentEvent));

        Assert.Contains("invalid or duplicate message", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtocolAcceptsStructuredProviderRetryDiagnostic()
    {
        var retry = new CopilotProviderRetryInfo(
            1,
            2,
            3,
            TimeSpan.FromMilliseconds(250),
            "timeout",
            null,
            "request-1");

        CopilotAgentEventProtocol.Validate(CopilotAgentEvent.FromProviderRetry(retry));
    }

    [Fact]
    public void ProtocolAcceptsStructuredProviderConnectionRecoveryDiagnostic()
    {
        var recovery = new CopilotProviderConnectionRecoveryInfo(
            1,
            TimeSpan.FromSeconds(5),
            "connection failure",
            "request-1");

        CopilotAgentEventProtocol.Validate(
            CopilotAgentEvent.FromProviderConnectionRecovery(recovery));
    }

    [Fact]
    public void ProtocolRejectsConflictingProviderRecoveryPayloads()
    {
        var retry = new CopilotProviderRetryInfo(
            1,
            2,
            3,
            TimeSpan.FromMilliseconds(250),
            "timeout",
            null);
        var recovery = new CopilotProviderConnectionRecoveryInfo(
            1,
            TimeSpan.FromSeconds(5),
            "connection failure");
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.RuntimeDiagnostic,
            Text = retry.ToDiagnosticText(),
            ProviderRetry = retry,
            ProviderConnectionRecovery = recovery,
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotAgentEventProtocol.Validate(agentEvent));

        Assert.Contains("cannot describe both", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsProviderRetryDiagnosticThatDisagreesWithMetadata()
    {
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.RuntimeDiagnostic,
            Text = "Retrying later.",
            ProviderRetry = new CopilotProviderRetryInfo(
                1,
                2,
                3,
                TimeSpan.Zero,
                "timeout",
                null),
        };

        var exception = Assert.Throws<InvalidOperationException>(() => Observe(agentEvent));

        Assert.Contains("mismatched metadata", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsNullProtocolCollection()
    {
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.Status,
            Text = "Working.",
            SteeringMessages = null!,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => Observe(agentEvent));

        Assert.Contains("null protocol collection", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState Observe(CopilotAgentEvent agentEvent)
    {
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Auto),
            new CopilotTurnStartedEvent(CopilotAgentMode.Auto));
        return CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));
    }

    private static CopilotToolExecutionInfo CreateExecution() => new()
    {
        CallId = "provider-call-1",
        Round = 1,
        Attempt = 1,
        MaxAttempts = 1,
        RuntimeName = "agent-framework",
        ToolName = "ProtectedTool",
        Access = CopilotToolAccess.Write,
        RiskLevel = CopilotToolRiskLevel.High,
        ApprovalMode = CopilotToolApprovalMode.Always,
        Idempotency = CopilotToolIdempotency.NonIdempotent,
        ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
        ConcurrencyKey = "resource:test",
        ArgumentSummary = "path=C:\\workspace\\file.txt",
        State = CopilotToolExecutionState.Running,
        StartedAtUtc = DateTimeOffset.UtcNow,
        TimeoutMs = 30_000,
    };
}
