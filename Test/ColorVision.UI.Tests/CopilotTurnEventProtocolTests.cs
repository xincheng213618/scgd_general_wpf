using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnEventProtocolTests
{
    [Fact]
    public void RejectsProgressBeforeStartedEvent()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Chat);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnRequestPreparedEvent(
                new CopilotPreparedTurnRequest("prepared", false))));

        Assert.Contains("before its started event", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDuplicateStartedEvent()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Chat);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnStartedEvent(CopilotAgentMode.Chat)));

        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsStartedEventForDifferentTurnId()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Chat, "turn:expected");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnStartedEvent("turn:other", CopilotAgentMode.Chat)));

        Assert.Contains("different turn ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsPreparedChatProgressAndMatchingCompletion()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Chat);
        var result = CreateChatResult();

        protocol.Observe(new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", true)));
        protocol.Observe(new CopilotTurnChatDeltaEvent(
            new CopilotStreamDelta(string.Empty, "partial")));
        protocol.Observe(new CopilotTurnProviderRetryEvent(
            new CopilotProviderRetryInfo(1, 2, 3, TimeSpan.Zero, "timeout", null)));
        protocol.Observe(new CopilotTurnCompletedEvent(result));

        Assert.Same(result, protocol.RequireCompletion());
    }

    [Fact]
    public void RejectsChatProgressBeforeRequestPreparation()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Chat);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(string.Empty, "partial"))));

        Assert.Contains("before its request was prepared", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateChatRequestPreparation()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Chat);
        var prepared = new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", false));
        protocol.Observe(prepared);

        var exception = Assert.Throws<InvalidOperationException>(() => protocol.Observe(prepared));

        Assert.Contains("more than once", exception.Message);
    }

    [Fact]
    public void RejectsAgentEventDuringChatTurn()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Chat);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnAgentEvent(
                CopilotAgentEvent.AnswerDelta("partial"))));

        Assert.Contains("chat turn cannot emit", exception.Message);
    }

    [Fact]
    public void RejectsChatEventDuringAgentTurn()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnRequestPreparedEvent(
                new CopilotPreparedTurnRequest("prepared", false))));

        Assert.Contains("Auto turn cannot emit", exception.Message);
    }

    [Fact]
    public void AcceptsAgentProgressAndMatchingCompletion()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Auto,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());

        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("partial")));
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()));
        protocol.Observe(new CopilotTurnPlanUpdatedEvent(
            CopilotTurnPlanSnapshot.FromTaskLedger(result.AgentRunResult!.TaskLedger)));
        protocol.Observe(new CopilotTurnCompletedEvent(result));

        Assert.Same(result, protocol.RequireCompletion());
    }

    [Fact]
    public void AcceptsOrderedHookLifecycleAndReconciledToolResult()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        var execution = CreateToolExecution(CopilotToolExecutionState.Pending);
        var hookRun = CopilotToolExecutionHookRun.Create(
            "module:policy",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Completed,
            12);
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Auto,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());

        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.HookStarted(
            execution,
            hookRun.SourceId,
            hookRun.Phase)));
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.HookCompleted(
            execution,
            hookRun)));
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult { ToolName = execution.ToolName, Success = true },
            CreateToolExecution(CopilotToolExecutionState.Completed),
            [hookRun])));
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()));
        protocol.Observe(new CopilotTurnPlanUpdatedEvent(
            CopilotTurnPlanSnapshot.FromTaskLedger(result.AgentRunResult!.TaskLedger)));
        protocol.Observe(new CopilotTurnCompletedEvent(result));

        Assert.Same(result, protocol.RequireCompletion());
    }

    [Fact]
    public void RejectsHookCompletionBeforeMatchingStart()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        var hookRun = CopilotToolExecutionHookRun.Create(
            "module:policy",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Completed,
            12);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.HookCompleted(
                CreateToolExecution(CopilotToolExecutionState.Pending),
                hookRun))));

        Assert.Contains("before it started", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsToolResultWhileHookIsActive()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        var execution = CreateToolExecution(CopilotToolExecutionState.Pending);
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.HookStarted(
            execution,
            "module:policy",
            CopilotToolExecutionHookPhase.BeforeExecute)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.FromToolResult(
                new CopilotToolResult { ToolName = execution.ToolName, Success = true },
                CreateToolExecution(CopilotToolExecutionState.Completed)))));

        Assert.Contains("before its active hook completed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsToolResultThatDropsObservedHookCompletion()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        var execution = CreateToolExecution(CopilotToolExecutionState.Pending);
        var hookRun = CopilotToolExecutionHookRun.Create(
            "module:policy",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Completed,
            12);
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.HookStarted(
            execution,
            hookRun.SourceId,
            hookRun.Phase)));
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.HookCompleted(
            execution,
            hookRun)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.FromToolResult(
                new CopilotToolResult { ToolName = execution.ToolName, Success = true },
                CreateToolExecution(CopilotToolExecutionState.Completed)))));

        Assert.Contains("did not reconcile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAgentTurnCompletionBeforeAgentCompletedItem()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Auto,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("partial")));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnCompletedEvent(result)));

        Assert.Contains("before its completed item", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAgentEventAfterAgentCompletedItem()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnAgentEvent(
                CopilotAgentEvent.RuntimeDiagnostic("late"))));

        Assert.Contains("after its completed item", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsCompletionForDifferentMode()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnCompletedEvent(CreateChatResult())));

        Assert.Contains("but Auto was requested", exception.Message);
    }

    [Fact]
    public void AcceptsInterruptedTerminalEventWithoutSuccessfulResult()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);

        protocol.Observe(CopilotTurnCompletedEvent.Interrupted(
            CopilotTurnStartedEvent.DefaultTurnId,
            CopilotAgentMode.Auto));

        var exception = Assert.Throws<InvalidOperationException>(protocol.RequireCompletion);
        Assert.Contains("Interrupted", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsInterruptedTerminalEventWithStructuredResult()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Chat);
        var result = CreateChatResult();
        protocol.Observe(new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", false)));

        protocol.Observe(CopilotTurnCompletedEvent.Interrupted(
            CopilotTurnStartedEvent.DefaultTurnId,
            result));

        Assert.Same(result, protocol.RequireCompletion());
    }

    [Fact]
    public void AcceptsFailedTerminalEventWithoutLeakingExceptionMessage()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        var error = CopilotTurnError.FromException(
            new InvalidOperationException("secret-provider-detail"));
        var errorEvent = new CopilotTurnErrorEvent(
            CopilotTurnStartedEvent.DefaultTurnId,
            CopilotAgentMode.Auto,
            error);
        var terminal = CopilotTurnCompletedEvent.Failed(
            CopilotTurnStartedEvent.DefaultTurnId,
            CopilotAgentMode.Auto,
            error);

        protocol.Observe(errorEvent);
        protocol.Observe(terminal);

        Assert.Equal("turn_failed", terminal.Error?.Code);
        Assert.Same(errorEvent.Error, terminal.Error);
        Assert.DoesNotContain("secret-provider-detail", terminal.Error?.Message, StringComparison.Ordinal);
        var exception = Assert.Throws<InvalidOperationException>(protocol.RequireCompletion);
        Assert.Contains("Failed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsFailedTerminalWithoutPrecedingErrorEvent()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(CopilotTurnCompletedEvent.Failed(
                CopilotTurnStartedEvent.DefaultTurnId,
                CopilotAgentMode.Auto,
                new InvalidOperationException("provider failed"))));

        Assert.Contains("invalid terminal metadata", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsProgressAfterErrorEventBeforeFailedTerminal()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);
        protocol.Observe(new CopilotTurnErrorEvent(
            CopilotTurnStartedEvent.DefaultTurnId,
            CopilotAgentMode.Auto,
            CopilotTurnError.FromException(new InvalidOperationException("provider failed"))));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnAgentEvent(
                CopilotAgentEvent.RuntimeDiagnostic("late"))));

        Assert.Contains("after its error event", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsEventAfterCompletion()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Chat);
        protocol.Observe(new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", false)));
        protocol.Observe(new CopilotTurnCompletedEvent(CreateChatResult()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(string.Empty, "late"))));

        Assert.Contains("after completion", exception.Message);
    }

    [Fact]
    public void RequiresCompletionBeforeReturningResult()
    {
        var protocol = CreateStartedProtocol(CopilotAgentMode.Auto);

        var exception = Assert.Throws<InvalidOperationException>(protocol.RequireCompletion);

        Assert.Contains("without a completion event", exception.Message);
    }

    private static CopilotTurnResult CreateChatResult()
    {
        return CopilotTurnResult.FromChat(
            CopilotTokenUsage.Empty,
            "prepared",
            chatAttachmentContextCaptured: false,
            new CopilotChatStreamResult(
                CopilotTokenUsage.Empty,
                CopilotChatFinishKind.Complete,
                "stop"));
    }

    private static CopilotTurnEventProtocol CreateStartedProtocol(CopilotAgentMode mode)
    {
        var protocol = new CopilotTurnEventProtocol(mode);
        protocol.Observe(new CopilotTurnStartedEvent(mode));
        return protocol;
    }

    private static CopilotToolExecutionInfo CreateToolExecution(CopilotToolExecutionState state)
    {
        return new CopilotToolExecutionInfo
        {
            CallId = "hook-protocol-call",
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "protocol-test",
            ToolName = "ProtocolTool",
            State = state,
            StartedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = state == CopilotToolExecutionState.Completed
                ? DateTimeOffset.UtcNow
                : null,
            TimeoutMs = 1_000,
        };
    }
}
