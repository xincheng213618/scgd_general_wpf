using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentTaskEventJournalIntegrityTests
{
    [Fact]
    public void StructurallyValidEventIdMustMatchItsIdentityFields()
    {
        var runId = CopilotAgentTaskEventIds.CreateRunId();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var mismatched = new CopilotAgentTaskEvent
        {
            Sequence = 1,
            Id = CopilotAgentTaskEventIds.CreateEventId(
                1,
                runId,
                CopilotAgentTaskEventType.RunStarted,
                occurredAtUtc),
            Type = CopilotAgentTaskEventType.RunStopped,
            OccurredAtUtc = occurredAtUtc,
            RunId = runId,
            SubjectId = runId,
            State = CopilotAgentStopReason.Completed.ToString(),
        };

        Assert.False(mismatched.IsStructurallyValid());
        Assert.False(new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [mismatched],
        }.IsStructurallyValid());
    }

    [Fact]
    public void JournalEquivalenceIncludesTheCompletePersistedEventPayload()
    {
        var runId = CopilotAgentTaskEventIds.CreateRunId();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var originalEvent = new CopilotAgentTaskEvent
        {
            Sequence = 1,
            Id = CopilotAgentTaskEventIds.CreateEventId(
                1,
                runId,
                CopilotAgentTaskEventType.ToolCompleted,
                occurredAtUtc),
            Type = CopilotAgentTaskEventType.ToolCompleted,
            OccurredAtUtc = occurredAtUtc,
            RunId = runId,
            SubjectId = CopilotAgentTaskEventIds.ForCall("same-call"),
            RelatedIds = [CopilotAgentTaskEventIds.ForApproval("same-action")],
            ToolName = "IntegrityTool",
            State = CopilotToolExecutionState.Completed.ToString(),
            Summary = "Original evidence.",
        };
        var rewrittenEvent = new CopilotAgentTaskEvent
        {
            Sequence = originalEvent.Sequence,
            Id = originalEvent.Id,
            Type = originalEvent.Type,
            OccurredAtUtc = originalEvent.OccurredAtUtc,
            RunId = originalEvent.RunId,
            SubjectId = originalEvent.SubjectId,
            RelatedIds = originalEvent.RelatedIds,
            ToolName = originalEvent.ToolName,
            State = originalEvent.State,
            Summary = "Rewritten evidence.",
        };
        var original = new CopilotAgentTaskEventJournalSnapshot { Events = [originalEvent] };
        var rewritten = new CopilotAgentTaskEventJournalSnapshot { Events = [rewrittenEvent] };

        Assert.True(original.IsStructurallyValid());
        Assert.True(rewritten.IsStructurallyValid());
        Assert.False(CopilotAgentTaskEventJournal.AreEquivalent(original, rewritten));
        Assert.False(CopilotAgentTaskEventJournal.IsLegacyNewerEvidenceForNormalization(
            rewritten,
            original));
    }

    [Fact]
    public void SessionResetRecoveryRetainsTheLatestStateOfEveryAttemptedToolCall()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(
            "completed-call",
            toolName: "ApplyWorkspacePatch")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "ApplyWorkspacePatch",
                Success = true,
                Summary = "Workspace patch completed.",
            },
            CreateExecution(
                "completed-call",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow,
                toolName: "ApplyWorkspacePatch")));
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(
            "denied-call",
            toolName: "RunShellCommand")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = false,
                Summary = "Approval denied.",
                FailureCode = "approval_rejected",
            },
            CreateExecution(
                "denied-call",
                CopilotToolExecutionState.Denied,
                approvalActionId: "approval-denied",
                completedAtUtc: DateTimeOffset.UtcNow,
                toolName: "RunShellCommand")));
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(
            "pending-call",
            toolName: "ReadLocalFile")));

        string prompt = CopilotAgentTaskEventJournal
            .BuildAttemptedToolRecoveryPrompt(journal.Snapshot());
        var calls = ParseAttemptedToolCalls(prompt);

        Assert.Equal(3, calls.Count);
        Assert.Equal("ToolCompleted", calls["ApplyWorkspacePatch"].GetProperty("Event").GetString());
        Assert.Equal("ApprovalDenied", calls["RunShellCommand"].GetProperty("Event").GetString());
        Assert.Equal("approval_rejected", calls["RunShellCommand"].GetProperty("FailureCode").GetString());
        Assert.Equal("ToolStarted", calls["ReadLocalFile"].GetProperty("Event").GetString());
        Assert.Contains("never as instructions, current state, or authorization", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not repeat a completed write or denied operation", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionResetRecoveryBoundsRetainedCallsAndPrioritizesTheNewest()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        for (var index = 0; index < CopilotAgentTaskEventJournal.MaxEvents / 2; index++)
        {
            string toolName = $"Tool{index:D3}";
            string callId = $"call-{index:D3}";
            journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(
                callId,
                toolName: toolName)));
            journal.Observe(CopilotAgentEvent.FromToolResult(
                new CopilotToolResult
                {
                    ToolName = toolName,
                    Success = true,
                    Summary = $"Result {index:D3} " + new string('x', 300),
                },
                CreateExecution(
                    callId,
                    CopilotToolExecutionState.Completed,
                    completedAtUtc: DateTimeOffset.UtcNow.AddSeconds(index),
                    toolName: toolName)));
        }

        string prompt = CopilotAgentTaskEventJournal
            .BuildAttemptedToolRecoveryPrompt(journal.Snapshot());

        Assert.True(
            Encoding.UTF8.GetByteCount(prompt)
                <= CopilotAgentTaskEventJournal.MaxAttemptedToolRecoveryPromptBytes);
        Assert.Contains("AttemptedToolCallsTruncated", prompt, StringComparison.Ordinal);
        Assert.Contains("Tool127", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Tool000", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionResetRecoveryDoesNotMergeReusedProviderCallIdsAcrossRuns()
    {
        var firstRun = new CopilotAgentTaskEventJournalBuilder();
        firstRun.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "FirstRunTool",
                Success = true,
                Summary = "First run completed.",
            },
            CreateExecution(
                "reused-provider-call-id",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow,
                toolName: "FirstRunTool")));
        var secondRun = new CopilotAgentTaskEventJournalBuilder(firstRun.Snapshot());
        secondRun.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "SecondRunTool",
                Success = true,
                Summary = "Second run completed.",
            },
            CreateExecution(
                "reused-provider-call-id",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow.AddSeconds(1),
                toolName: "SecondRunTool")));

        string prompt = CopilotAgentTaskEventJournal
            .BuildAttemptedToolRecoveryPrompt(secondRun.Snapshot());
        var calls = ParseAttemptedToolCalls(prompt);

        Assert.Equal(2, calls.Count);
        Assert.Contains("FirstRunTool", calls.Keys);
        Assert.Contains("SecondRunTool", calls.Keys);
    }

    [Fact]
    public void SessionResetRecoveryRedactsPersistedSummariesAgainAtPromptTime()
    {
        const string credential = "sk-abcdefghijklmnopqrstuvwxyz123456";
        var runId = CopilotAgentTaskEventIds.CreateRunId();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var toolEvent = new CopilotAgentTaskEvent
        {
            Sequence = 1,
            Id = CopilotAgentTaskEventIds.CreateEventId(
                1,
                runId,
                CopilotAgentTaskEventType.ToolCompleted,
                occurredAtUtc),
            Type = CopilotAgentTaskEventType.ToolCompleted,
            OccurredAtUtc = occurredAtUtc,
            RunId = runId,
            SubjectId = CopilotAgentTaskEventIds.ForCall("crafted-call"),
            ToolName = "CraftedTool",
            State = CopilotToolExecutionState.Completed.ToString(),
            Summary = $"Provider returned {credential}.",
        };
        var snapshot = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [toolEvent],
        };
        Assert.True(snapshot.IsStructurallyValid());

        string prompt = CopilotAgentTaskEventJournal
            .BuildAttemptedToolRecoveryPrompt(snapshot);
        var calls = ParseAttemptedToolCalls(prompt);

        Assert.Equal(
            "Provider returned <redacted>.",
            calls["CraftedTool"].GetProperty("Summary").GetString());
        Assert.DoesNotContain(credential, prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void InterruptedStopClosesEveryDanglingToolBeforeRunStop()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("call-1")));
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("call-2")));

        journal.RecordStop(CopilotAgentStopReason.Interrupted);

        var snapshot = journal.Snapshot();
        Assert.True(snapshot.IsStructurallyValid());
        var terminalEvents = snapshot.Events
            .Where(item => item.Type == CopilotAgentTaskEventType.ToolCompleted)
            .ToArray();
        Assert.Collection(
            terminalEvents,
            item => AssertSyntheticTerminal(item, "call-1", CopilotToolExecutionState.Interrupted, CopilotToolFailureCode.OutcomeUnknown),
            item => AssertSyntheticTerminal(item, "call-2", CopilotToolExecutionState.Interrupted, CopilotToolFailureCode.OutcomeUnknown));
        var stopped = Assert.Single(snapshot.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.All(terminalEvents, item => Assert.True(item.Sequence < stopped.Sequence));
        var recoveryPrompt = CopilotAgentTaskEventJournal.BuildAttemptedToolRecoveryPrompt(snapshot);
        Assert.Contains("external outcome is unknown", recoveryPrompt, StringComparison.Ordinal);
        Assert.Contains("do not retry a write or non-idempotent operation blindly", recoveryPrompt, StringComparison.Ordinal);
        Assert.Contains(CopilotToolFailureCode.OutcomeUnknown, recoveryPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelledStopClosesDanglingToolAsCancelled()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("cancelled-call")));

        journal.RecordStop(CopilotAgentStopReason.Cancelled);

        var terminal = Assert.Single(
            journal.Snapshot().Events,
            item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        AssertSyntheticTerminal(
            terminal,
            "cancelled-call",
            CopilotToolExecutionState.Cancelled,
            "tool_execution_cancelled");
    }

    [Fact]
    public void AuthoritativeToolResultIsNotDuplicatedAtStop()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("completed-call")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "IntegrityTool",
                Success = true,
                Summary = "Tool completed.",
            },
            CreateExecution(
                "completed-call",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow)));

        journal.RecordStop(CopilotAgentStopReason.Completed);

        var terminal = Assert.Single(
            journal.Snapshot().Events,
            item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Equal(CopilotToolExecutionState.Completed.ToString(), terminal.State);
        Assert.Equal(string.Empty, terminal.FailureCode);
        Assert.Equal("Tool completed.", terminal.Summary);
    }

    [Fact]
    public void BackgroundCompletionKeepsStructuredExitCodeAndAcceptsLegacyMissingField()
    {
        var completedAtUtc = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordBackgroundShellCommandCompletion(new CopilotBackgroundShellCommandSnapshot(
            "background-command",
            "conversation",
            "task",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "private command",
            new string('a', 64),
            completedAtUtc.AddMinutes(-1),
            completedAtUtc,
            ProcessId: 42,
            ProcessTreeContained: true,
            State: CopilotBackgroundShellCommandState.Failed,
            ExitCode: 23,
            StandardOutput: string.Empty,
            StandardError: string.Empty));
        journal.RecordStop(CopilotAgentStopReason.Completed);
        var snapshot = journal.Snapshot();
        var completion = Assert.Single(
            snapshot.Events,
            item => item.Type == CopilotAgentTaskEventType.BackgroundCommandCompleted);

        Assert.Equal(23, completion.ExitCode);
        Assert.True(snapshot.IsStructurallyValid());

        var legacyJson = JObject.FromObject(snapshot);
        foreach (var item in Assert.IsType<JArray>(legacyJson[nameof(CopilotAgentTaskEventJournalSnapshot.Events)]))
            Assert.IsType<JObject>(item).Remove(nameof(CopilotAgentTaskEvent.ExitCode));
        var legacy = legacyJson.ToObject<CopilotAgentTaskEventJournalSnapshot>();

        Assert.NotNull(legacy);
        Assert.True(legacy.IsStructurallyValid());
        Assert.Null(Assert.Single(
            legacy.Events,
            item => item.Type == CopilotAgentTaskEventType.BackgroundCommandCompleted).ExitCode);

        var invalidToolEvent = new CopilotAgentTaskEvent
        {
            Sequence = completion.Sequence,
            Id = completion.Id,
            Type = CopilotAgentTaskEventType.ToolCompleted,
            OccurredAtUtc = completion.OccurredAtUtc,
            RunId = completion.RunId,
            SubjectId = completion.SubjectId,
            RelatedIds = completion.RelatedIds,
            ToolName = "RunShellCommand",
            State = CopilotToolExecutionState.Completed.ToString(),
            ExitCode = 0,
            Summary = "Tool completed.",
        };
        Assert.False(invalidToolEvent.IsStructurallyValid());

        var contradictoryCompletion = new CopilotAgentTaskEvent
        {
            Sequence = completion.Sequence,
            Id = completion.Id,
            Type = CopilotAgentTaskEventType.BackgroundCommandCompleted,
            OccurredAtUtc = completion.OccurredAtUtc,
            RunId = completion.RunId,
            SubjectId = completion.SubjectId,
            RelatedIds = completion.RelatedIds,
            State = "completed",
            ExitCode = 23,
            Summary = "Background command completed.",
        };
        Assert.False(contradictoryCompletion.IsStructurallyValid());
    }

    [Fact]
    public void BackgroundToolResultsLinkHashedCommandAndRecordTerminalStateOnce()
    {
        const string backgroundId = "bg:private-background-command-id";
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(
            "start-background-call",
            toolName: "StartBackgroundShellCommand")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "StartBackgroundShellCommand",
                Success = true,
                Summary = "Background command started.",
                BackgroundShellCommands =
                [
                    new CopilotBackgroundShellCommandEvidence(
                        backgroundId,
                        CopilotBackgroundShellCommandState.Running,
                        ExitCode: null),
                ],
            },
            CreateExecution(
                "start-background-call",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow,
                toolName: "StartBackgroundShellCommand")));
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(
            "stop-background-call",
            toolName: "StopBackgroundShellCommand")));
        var stoppedResult = new CopilotToolResult
        {
            ToolName = "StopBackgroundShellCommand",
            Success = true,
            Summary = "Background command stopped.",
            BackgroundShellCommands =
            [
                new CopilotBackgroundShellCommandEvidence(
                    backgroundId,
                    CopilotBackgroundShellCommandState.Stopped,
                    ExitCode: null),
            ],
        };
        journal.Observe(CopilotAgentEvent.FromToolResult(
            stoppedResult,
            CreateExecution(
                "stop-background-call",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow,
                toolName: "StopBackgroundShellCommand")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            stoppedResult,
            CreateExecution(
                "duplicate-stop-observation",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow,
                toolName: "StopBackgroundShellCommand")));
        journal.RecordStop(CopilotAgentStopReason.Completed);

        var snapshot = journal.Snapshot();
        var expectedSubject =
            CopilotAgentTaskEventIds.ForBackgroundCommand(backgroundId);
        var startCompleted = Assert.Single(snapshot.Events, item =>
            item.Type == CopilotAgentTaskEventType.ToolCompleted
            && string.Equals(
                item.ToolName,
                "StartBackgroundShellCommand",
                StringComparison.Ordinal));
        Assert.Contains(expectedSubject, startCompleted.RelatedIds);
        Assert.DoesNotContain(backgroundId, startCompleted.RelatedIds);
        var terminal = Assert.Single(snapshot.Events, item =>
            item.Type
                == CopilotAgentTaskEventType.BackgroundCommandCompleted);
        Assert.Equal(expectedSubject, terminal.SubjectId);
        Assert.Equal("stopped", terminal.State);
        Assert.Null(terminal.ExitCode);
        Assert.True(snapshot.IsStructurallyValid());
    }

    [Fact]
    public void RunStartLinksHashedActiveBackgroundCommandsWithoutRawIds()
    {
        const string backgroundId = "bg:private-inherited-background-id";
        var journal = new CopilotAgentTaskEventJournalBuilder();

        journal.RecordRunStarted(
        [
            CreateBackgroundCommandSnapshot(backgroundId),
        ]);
        journal.RecordStop(CopilotAgentStopReason.Completed);

        var snapshot = journal.Snapshot();
        var runStarted = Assert.Single(snapshot.Events, item =>
            item.Type == CopilotAgentTaskEventType.RunStarted);
        var expectedSubject =
            CopilotAgentTaskEventIds.ForBackgroundCommand(backgroundId);
        Assert.Equal([expectedSubject], runStarted.RelatedIds);
        Assert.Contains(
            "1 active application-managed background command",
            runStarted.Summary,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            backgroundId,
            JsonSerializer.Serialize(snapshot),
            StringComparison.Ordinal);
        Assert.True(snapshot.IsStructurallyValid());
    }

    [Fact]
    public async Task AgentRuntimeCapturesActiveBackgroundCommandAtRunStart()
    {
        const string conversationId = "runtime-background-conversation";
        const string backgroundId = "bg:private-runtime-background-id";
        var snapshotRequests = new List<string?>();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => new SingleResponseChatClient(),
            new EmptyExternalToolProvider(),
            new CopilotCapabilityCatalog(),
            new CopilotCodexStopHookExecutor(),
            requestedConversationId =>
            {
                snapshotRequests.Add(requestedConversationId);
                return
                [
                    CreateBackgroundCommandSnapshot(backgroundId),
                ];
            });
        var request = new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = "runtime-background-task",
            WorkspacePath = System.IO.Path.GetTempPath(),
            UserText = "Report the current task state.",
            TaskIntentText = "Report the current task state.",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Chat,
            HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
                CopilotCodexApprovalPolicyMode.Untrusted),
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 16_384,
                MaxToolCalls = 1,
                MaxAgentPasses = 1,
                TotalDuration = TimeSpan.FromSeconds(30),
            },
        };

        var result = await runtime.RunAsync(
            request,
            _ => { },
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal([conversationId], snapshotRequests);
        var runStarted = Assert.Single(result.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.RunStarted
            && string.Equals(
                item.RunId,
                result.TaskEventJournal.Events[^1].RunId,
                StringComparison.Ordinal));
        Assert.Equal(
            [CopilotAgentTaskEventIds.ForBackgroundCommand(backgroundId)],
            runStarted.RelatedIds);
        Assert.DoesNotContain(
            backgroundId,
            JsonSerializer.Serialize(result.TaskEventJournal),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            backgroundId,
            result.PreparedUserMessageContent,
            StringComparison.Ordinal);
        Assert.True(result.TaskEventJournal.IsStructurallyValid());
    }

    [Fact]
    public void ValidationSnapshotAndAuditSpineSurviveBackgroundOutputRollOver()
    {
        const string backgroundId = "bg:private-validation-rollover-id";
        const string validationCallId = "validation-rollover-call";
        var activeBackgroundCommand =
            CreateBackgroundCommandSnapshot(backgroundId);
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted([activeBackgroundCommand]);
        journal.RecordValidationBackgroundCommandSnapshot(
            validationCallId,
            [activeBackgroundCommand]);
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(
            validationCallId,
            toolName: "RunWorkspaceValidation")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "RunWorkspaceValidation",
                Success = true,
                Summary = "Workspace validation completed.",
                ProcessOperation = "test",
                ProcessExitCode = 0,
            },
            CreateExecution(
                validationCallId,
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow,
                toolName: "RunWorkspaceValidation")));
        for (var index = 0;
            index < CopilotAgentTaskEventJournal.MaxEvents + 32;
            index++)
        {
            journal.RecordBackgroundShellCommandOutput(
                CreateBackgroundOutputEvent(backgroundId, index));
        }
        journal.RecordStop(CopilotAgentStopReason.Completed);

        var snapshot = journal.Snapshot();
        var validationSubject =
            CopilotAgentTaskEventIds.ForCall(validationCallId);
        var expectedBackgroundSubject =
            CopilotAgentTaskEventIds.ForBackgroundCommand(backgroundId);

        Assert.Equal(CopilotAgentTaskEventJournal.MaxEvents, snapshot.Events.Count);
        Assert.Contains(snapshot.Events, item =>
            item.Type == CopilotAgentTaskEventType.RunStarted
            && string.Equals(item.RunId, journal.RunId, StringComparison.Ordinal));
        var validationSnapshot = Assert.Single(snapshot.Events, item =>
            item.Type == CopilotAgentTaskEventType.EvidenceCaptured
            && string.Equals(
                item.State,
                CopilotAgentTaskEventJournal.ValidationBackgroundSnapshotState,
                StringComparison.Ordinal));
        Assert.Equal(validationSubject, validationSnapshot.SubjectId);
        Assert.Equal([expectedBackgroundSubject], validationSnapshot.RelatedIds);
        Assert.Contains(snapshot.Events, item =>
            item.Type == CopilotAgentTaskEventType.ToolStarted
            && string.Equals(item.SubjectId, validationSubject, StringComparison.Ordinal));
        Assert.Contains(snapshot.Events, item =>
            item.Type == CopilotAgentTaskEventType.ToolCompleted
            && string.Equals(item.SubjectId, validationSubject, StringComparison.Ordinal));
        Assert.DoesNotContain(
            backgroundId,
            JsonSerializer.Serialize(snapshot),
            StringComparison.Ordinal);
        Assert.True(snapshot.IsStructurallyValid());
    }

    [Fact]
    public async Task AgentRuntimeCapturesActiveBackgroundCommandsWhenValidationStarts()
    {
        const string conversationId = "runtime-validation-conversation";
        const string backgroundId = "bg:private-runtime-validation-id";
        var snapshotRequests = new List<string?>();
        var chatClient = new ValidationCallingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([new ValidationProbeTool()]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => chatClient,
            new EmptyExternalToolProvider(),
            new CopilotCapabilityCatalog(),
            new CopilotCodexStopHookExecutor(),
            requestedConversationId =>
            {
                snapshotRequests.Add(requestedConversationId);
                return snapshotRequests.Count == 1
                    ? Array.Empty<CopilotBackgroundShellCommandSnapshot>()
                    : [CreateBackgroundCommandSnapshot(backgroundId)];
            });
        var request = new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = "runtime-validation-task",
            WorkspacePath = System.IO.Path.GetTempPath(),
            UserText = "Run the bounded workspace validation probe.",
            TaskIntentText = "Run the bounded workspace validation probe.",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Code,
            HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
                CopilotCodexApprovalPolicyMode.Untrusted),
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 16_384,
                MaxToolCalls = 1,
                MaxAgentPasses = 1,
                TotalDuration = TimeSpan.FromSeconds(30),
            },
        };

        var result = await runtime.RunAsync(
            request,
            _ => { },
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal([conversationId, conversationId], snapshotRequests);
        var validationSnapshot = Assert.Single(result.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.EvidenceCaptured
            && string.Equals(
                item.State,
                CopilotAgentTaskEventJournal.ValidationBackgroundSnapshotState,
                StringComparison.Ordinal));
        Assert.Equal("RunWorkspaceValidation", validationSnapshot.ToolName);
        Assert.Equal(
            [CopilotAgentTaskEventIds.ForBackgroundCommand(backgroundId)],
            validationSnapshot.RelatedIds);
        Assert.DoesNotContain(
            backgroundId,
            JsonSerializer.Serialize(result.TaskEventJournal),
            StringComparison.Ordinal);
        Assert.True(result.TaskEventJournal.IsStructurallyValid());
    }

    [Fact]
    public async Task AgentRuntimeFinalizesStoppedPostToolHookAsPolicyBlocker()
    {
        var hookRunner = new PostToolStoppingHookRunner();
        var chatClient = new ValidationCallingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([new ValidationProbeTool()]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(
                Array.Empty<ICopilotToolExecutionHook>(),
                utcNow: null,
                hookPhaseTimeout: TimeSpan.FromSeconds(2),
                progressInterval: TimeSpan.FromSeconds(1),
                codexCommandHookRunner: hookRunner),
            _ => chatClient,
            new EmptyExternalToolProvider(),
            new CopilotCapabilityCatalog(),
            new CopilotCodexStopHookExecutor());
        var fingerprint = new string('a', 64);
        var request = new CopilotAgentRequest
        {
            ConversationId = "runtime-post-tool-stop-conversation",
            TaskId = "runtime-post-tool-stop-task",
            WorkspacePath = System.IO.Path.GetTempPath(),
            UserText = "Run the validation probe and obey its post-tool policy.",
            TaskIntentText = "Run the validation probe and obey its post-tool policy.",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Code,
            HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexHooksEnabled = true,
            CodexPluginsEnabled = true,
            CodexCommandHooks =
            [
                new CopilotCodexCommandHookDefinition(
                    "codex-config:" + fingerprint[..32],
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "runtime-post-tool-hooks.json"),
                    CopilotProjectInstructionConfigSources.CodexHome,
                    CopilotCodexConfiguredHookEvent.PostToolUse,
                    "^RunWorkspaceValidation$",
                    "test-command",
                    5,
                    string.Empty,
                    CopilotToolExecutionHookMode.Sync,
                    0,
                    fingerprint),
            ],
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
                CopilotCodexApprovalPolicyMode.Untrusted),
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 16_384,
                MaxToolCalls = 2,
                MaxAgentPasses = 2,
                TotalDuration = TimeSpan.FromSeconds(30),
            },
        };

        var result = await runtime.RunAsync(request, _ => { }, CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Blocked, result.StopReason);
        Assert.Equal(1, chatClient.CallCount);
        Assert.Equal(1, hookRunner.CallCount);
        Assert.True(Assert.Single(result.StepRecords).Observation.Success);
        var blocker = Assert.Single(result.Blockers, item =>
            string.Equals(item.Code, "post_tool_hook_stopped", StringComparison.Ordinal));
        Assert.Equal(CopilotAgentBlockerKind.Policy, blocker.Kind);
        Assert.True(blocker.IsStructurallyValid());
        Assert.Contains(result.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.BlockerDetected
            && string.Equals(item.State, "post_tool_hook_stopped", StringComparison.Ordinal));
        Assert.True(result.TaskEventJournal.IsStructurallyValid());
    }

    [Fact]
    public void ApprovalDenialIsTerminalWithoutSyntheticToolCompletion()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("denied-call")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "IntegrityTool",
                Success = false,
                Summary = "Approval denied.",
                FailureCode = "approval_rejected",
            },
            CreateExecution(
                "denied-call",
                CopilotToolExecutionState.Denied,
                approvalActionId: "approval-denied",
                completedAtUtc: DateTimeOffset.UtcNow)));

        journal.RecordStop(CopilotAgentStopReason.ApprovalDenied);

        var snapshot = journal.Snapshot();
        Assert.DoesNotContain(snapshot.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        var denied = Assert.Single(
            snapshot.Events,
            item => item.Type == CopilotAgentTaskEventType.ApprovalDenied);
        Assert.Contains(CopilotAgentTaskEventIds.ForCall("denied-call"), denied.RelatedIds);
        Assert.Equal("approval_rejected", denied.FailureCode);
    }

    [Fact]
    public void ApprovalRequestClosesTheInitialAttemptWithoutUnknownOutcome()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("approval-call")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "IntegrityTool",
                Success = true,
                Summary = "Approval required.",
            },
            CreateExecution(
                "approval-call",
                CopilotToolExecutionState.AwaitingApproval,
                approvalActionId: "approval-action",
                completedAtUtc: DateTimeOffset.UtcNow)));

        journal.RecordStop(CopilotAgentStopReason.Interrupted);

        var snapshot = journal.Snapshot();
        Assert.DoesNotContain(snapshot.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        var approval = Assert.Single(snapshot.Events, item => item.Type == CopilotAgentTaskEventType.ApprovalRequested);
        Assert.Equal(CopilotToolExecutionState.AwaitingApproval.ToString(), approval.State);
        var recoveryPrompt = CopilotAgentTaskEventJournal.BuildAttemptedToolRecoveryPrompt(snapshot);
        Assert.Contains("protected operation did not execute", recoveryPrompt, StringComparison.Ordinal);
        var recovered = Assert.Single(ParseAttemptedToolCalls(recoveryPrompt)).Value;
        Assert.Equal(CopilotToolExecutionState.AwaitingApproval.ToString(), recovered.GetProperty("State").GetString());
        Assert.Equal(string.Empty, recovered.GetProperty("FailureCode").GetString());
    }

    [Fact]
    public void RunStopClosesOnlyDanglingStructuredUserQuestions()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var answered = CreatePendingQuestion(journal.RunId, "question:11111111111111111111111111111111");
        var interrupted = CreatePendingQuestion(journal.RunId, "question:22222222222222222222222222222222");
        journal.Observe(CopilotAgentEvent.UserQuestionRequested(answered));
        journal.Observe(CopilotAgentEvent.UserQuestionResolved(
            answered.Resolve(CopilotUserQuestionResolution.Answered, "Option A")));
        journal.Observe(CopilotAgentEvent.UserQuestionRequested(interrupted));

        journal.RecordStop(CopilotAgentStopReason.Interrupted);

        var snapshot = journal.Snapshot();
        var resolutions = snapshot.Events
            .Where(item => item.Type == CopilotAgentTaskEventType.UserQuestionResolved)
            .ToArray();
        Assert.Equal(2, resolutions.Length);
        Assert.Single(resolutions, item =>
            string.Equals(
                item.SubjectId,
                CopilotAgentTaskEventIds.ForUserQuestion(answered.RequestId),
                StringComparison.Ordinal)
            && string.Equals(item.State, CopilotUserQuestionResolution.Answered.ToString(), StringComparison.Ordinal));
        var synthetic = Assert.Single(resolutions, item =>
            string.Equals(
                item.SubjectId,
                CopilotAgentTaskEventIds.ForUserQuestion(interrupted.RequestId),
                StringComparison.Ordinal));
        Assert.Equal(CopilotUserQuestionResolution.Cancelled.ToString(), synthetic.State);
        Assert.Contains("closed without an answer", synthetic.Summary, StringComparison.Ordinal);
        var stopped = Assert.Single(snapshot.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.True(synthetic.Sequence < stopped.Sequence);
        Assert.True(snapshot.IsStructurallyValid());
    }

    [Fact]
    public void RunStopIsIdempotentButCannotBeRewritten()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();

        journal.RecordStop(CopilotAgentStopReason.Completed);
        journal.RecordStop(CopilotAgentStopReason.Completed);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            journal.RecordStop(CopilotAgentStopReason.Interrupted));

        var stopped = Assert.Single(
            journal.Snapshot().Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.Equal(CopilotAgentStopReason.Completed.ToString(), stopped.State);
        Assert.Contains("already stopped", exception.Message, StringComparison.Ordinal);
        Assert.True(journal.Snapshot().IsStructurallyValid());

        var duplicateTimestamp = stopped.OccurredAtUtc.AddTicks(1);
        var duplicate = new CopilotAgentTaskEvent
        {
            Sequence = stopped.Sequence + 1,
            Id = CopilotAgentTaskEventIds.CreateEventId(
                stopped.Sequence + 1,
                stopped.RunId,
                CopilotAgentTaskEventType.RunStopped,
                duplicateTimestamp),
            Type = CopilotAgentTaskEventType.RunStopped,
            OccurredAtUtc = duplicateTimestamp,
            RunId = stopped.RunId,
            SubjectId = stopped.SubjectId,
            State = stopped.State,
            Summary = stopped.Summary,
        };
        var duplicatedSnapshot = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = journal.Snapshot().Events.Append(duplicate).ToArray(),
        };
        Assert.True(duplicate.IsStructurallyValid());
        Assert.False(duplicatedSnapshot.IsStructurallyValid());
    }

    [Fact]
    public void InterruptedRunRecoveryRepairsCheckpointJournalBeforeRunStop()
    {
        var profile = CreateProfile();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("crashed-call")));
        var pendingQuestion = CreatePendingQuestion(
            journal.RunId,
            "question:33333333333333333333333333333333");
        journal.Observe(CopilotAgentEvent.UserQuestionRequested(pendingQuestion));
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            CopilotCapabilityCatalog.Shared.GetSnapshot(),
            taskEventJournal: journal.Snapshot());
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(checkpoint);
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
            IsExecutionInProgress = true,
        };
        conversation.Messages.Add(assistant);

        Assert.True(CopilotInterruptedAgentRunRecovery.Normalize(conversation, assistant));

        var recovered = Assert.IsType<CopilotAgentSessionCheckpoint>(conversation.AgentSessionCheckpoint);
        var terminal = Assert.Single(
            recovered.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        AssertSyntheticTerminal(
            terminal,
            "crashed-call",
            CopilotToolExecutionState.Interrupted,
            CopilotToolFailureCode.OutcomeUnknown);
        var questionResolution = Assert.Single(
            recovered.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.UserQuestionResolved);
        Assert.Equal(
            CopilotAgentTaskEventIds.ForUserQuestion(pendingQuestion.RequestId),
            questionResolution.SubjectId);
        Assert.Equal(CopilotUserQuestionResolution.Cancelled.ToString(), questionResolution.State);
        var stopped = Assert.Single(
            recovered.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.True(terminal.Sequence < stopped.Sequence);
        Assert.True(questionResolution.Sequence < stopped.Sequence);
        Assert.Equal(CopilotAgentStopReason.Interrupted, assistant.AgentStopReason);
    }

    [Fact]
    public void InterruptedRunRecoverySkipsNullLegacyMessagesWhenInferringMode()
    {
        var profile = CreateProfile();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            CopilotCapabilityCatalog.Shared.GetSnapshot(),
            taskEventJournal: journal.Snapshot());
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(checkpoint);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Continue")
        {
            RequestMode = CopilotAgentMode.Auto,
        });
        conversation.Messages.Add(null!);
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Chat,
            IsExecutionInProgress = true,
        };
        conversation.Messages.Add(assistant);

        Assert.True(CopilotInterruptedAgentRunRecovery.Normalize(conversation, assistant));

        Assert.Equal(CopilotAgentMode.Auto, assistant.RequestMode);
        Assert.Equal(CopilotAgentStopReason.Interrupted, assistant.AgentStopReason);
    }

    private static CopilotBackgroundShellCommandSnapshot
        CreateBackgroundCommandSnapshot(string backgroundId)
    {
        return new CopilotBackgroundShellCommandSnapshot(
            backgroundId,
            "conversation",
            "task",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "private command",
            new string('a', 64),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedAtUtc: null,
            ProcessId: 42,
            ProcessTreeContained: true,
            State: CopilotBackgroundShellCommandState.Running,
            ExitCode: null,
            StandardOutput: string.Empty,
            StandardError: string.Empty);
    }

    private static CopilotUserQuestionSnapshot CreatePendingQuestion(
        string taskId,
        string requestId)
    {
        return new CopilotUserQuestionSnapshot
        {
            RequestId = requestId,
            ConversationId = "conversation:journal-integrity",
            TaskId = taskId,
            Header = "Choice",
            Question = "Which path should the Agent use?",
            Options =
            [
                new CopilotUserQuestionOption
                {
                    RequestId = requestId,
                    TaskId = taskId,
                    Label = "Option A",
                    Description = "Use the first path.",
                },
                new CopilotUserQuestionOption
                {
                    RequestId = requestId,
                    TaskId = taskId,
                    Label = "Option B",
                    Description = "Use the second path.",
                },
            ],
            RequestedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static CopilotBackgroundShellOutputMonitorEventArgs
        CreateBackgroundOutputEvent(string backgroundId, int index)
    {
        var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new CopilotBackgroundShellOutputMonitorEventArgs(
            new CopilotBackgroundShellOutputMonitorSnapshot(
                $"monitor-{index}",
                "conversation",
                backgroundId,
                CopilotBackgroundShellOutputStream.StandardOutput,
                "private monitor description",
                startedAtUtc,
                startedAtUtc.AddHours(1),
                CopilotBackgroundShellOutputMonitorState.Running,
                PublishedEvents: index,
                SuppressedEvents: 0),
            $"private output {index}",
            suppressedEvents: 0);
    }

    private static CopilotToolExecutionInfo CreateExecution(
        string callId,
        CopilotToolExecutionState state = CopilotToolExecutionState.Running,
        string approvalActionId = "",
        DateTimeOffset? completedAtUtc = null,
        string toolName = "IntegrityTool")
    {
        return new CopilotToolExecutionInfo
        {
            CallId = callId,
            Round = 1,
            RuntimeName = "IntegrityRuntime",
            ToolName = toolName,
            State = state,
            ApprovalActionId = approvalActionId,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAtUtc = completedAtUtc,
        };
    }

    private static Dictionary<string, JsonElement> ParseAttemptedToolCalls(string prompt)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (string line in prompt.Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith('{'))
                continue;
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("Type", out var type)
                || !string.Equals(type.GetString(), "AttemptedToolCall", StringComparison.Ordinal))
            {
                continue;
            }

            result[root.GetProperty("ToolName").GetString()!] = root.Clone();
        }
        return result;
    }

    private static void AssertSyntheticTerminal(
        CopilotAgentTaskEvent item,
        string callId,
        CopilotToolExecutionState expectedState,
        string expectedFailureCode)
    {
        Assert.Equal(CopilotAgentTaskEventIds.ForCall(callId), item.SubjectId);
        Assert.Equal("IntegrityTool", item.ToolName);
        Assert.Equal(expectedState.ToString(), item.State);
        Assert.Equal(expectedFailureCode, item.FailureCode);
        Assert.Contains("before a terminal result was recorded", item.Summary, StringComparison.Ordinal);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }

    private sealed class EmptyExternalToolProvider :
        ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(
            CopilotAgentRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotExternalToolLease());
        }
    }

    private sealed class SingleResponseChatClient : IChatClient
    {
        private const string Response =
            "The current task state has been recorded with bounded structured evidence.";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, Response))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                Response)
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class ValidationProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "RunWorkspaceValidation";

        public string Description =>
            "Returns deterministic validation evidence for runtime journal tests.";

        public bool CanHandle(CopilotAgentRequest request) => true;

        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Validation probe completed.",
                ProcessOperation = "test",
                ProcessExitCode = 0,
            });
        }
    }

    private sealed class ValidationCallingChatClient : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref _callCount);
            return Task.FromResult(call == 1
                ? new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    [CreateValidationCall()]))
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                }
                : new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    "Validation probe finished."))
                {
                    FinishReason = ChatFinishReason.Stop,
                });
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref _callCount);
            await Task.CompletedTask;
            if (call == 1)
            {
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [CreateValidationCall()])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
                yield break;
            }

            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                "Validation probe finished.")
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private static FunctionCallContent CreateValidationCall() =>
            new(
                "runtime-validation-call",
                "colorvision_run_workspace_validation",
                new Dictionary<string, object?>());
    }

    private sealed class PostToolStoppingHookRunner : ICopilotCodexCommandHookRunner
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"continue":false,"reason":"validation policy requires review"}""",
                string.Empty));
        }
    }
}
