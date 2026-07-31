using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskEventDiagnosticsTests
{
    [Fact]
    public void TaskLogCommandExposesLimitAndErrorsArgumentsDuringAnActiveRun()
    {
        var invocation = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/task-log errors"));
        var suggestion = Assert.Single(CopilotLocalCommandCatalog.Suggest("/task-log "));

        Assert.Equal(CopilotLocalCommandKind.TaskLog, invocation.Command.Kind);
        Assert.Equal("errors", invocation.Arguments);
        Assert.Equal("/task-log [N|errors]", invocation.Command.Usage);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal("/task-log errors", suggestion.Name);
    }

    [Theory]
    [InlineData(null, 0, 20)]
    [InlineData("", 0, 20)]
    [InlineData("1", 0, 1)]
    [InlineData("100", 0, 100)]
    [InlineData("errors", 1, 20)]
    [InlineData("ERRORS", 1, 20)]
    [InlineData("0", 2, 0)]
    [InlineData("101", 2, 0)]
    [InlineData("errors 5", 2, 0)]
    [InlineData("all", 2, 0)]
    public void TaskLogCommandAcceptsABoundedLimitOrFailureFilter(
        string? arguments,
        int expectedAction,
        int expectedLimit)
    {
        var request = CopilotAgentTaskEventDiagnostics.ParseCommand(arguments);

        Assert.Equal((CopilotAgentTaskEventDiagnosticAction)expectedAction, request.Action);
        Assert.Equal(expectedLimit, request.Limit);
    }

    [Fact]
    public void FormatShowsLatestEventsWithoutRawIdentifiersOrSecrets()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(new CopilotToolExecutionInfo
        {
            CallId = "raw-call-id",
            RuntimeName = "TestRuntime",
            ToolName = "InspectWorkspace",
            State = CopilotToolExecutionState.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
        }));
        journal.RecordBlocker(new CopilotAgentBlockerSnapshot
        {
            Code = "provider_error",
            Summary = "Request failed with api_key=super-secret.",
            ToolName = "InspectWorkspace",
        });
        journal.RecordStop(CopilotAgentStopReason.Blocked);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Diagnostics";
        conversation.LatestAgentTaskEventJournal = journal.Snapshot();

        var report = CopilotAgentTaskEventDiagnostics.Format(conversation);

        Assert.StartsWith("Agent 任务日志 · Diagnostics", report);
        Assert.Contains("RunStopped", report);
        Assert.Contains("BlockerDetected · InspectWorkspace · provider_error", report);
        Assert.Contains("ToolStarted · InspectWorkspace · Running", report);
        Assert.DoesNotContain("raw-call-id", report, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", report, StringComparison.Ordinal);
        Assert.Contains("<redacted>", report, StringComparison.Ordinal);
        Assert.Contains("不包含工具参数、模型隐藏推理或授权凭据", report);
    }

    [Fact]
    public void FormatHonorsAnExplicitRecentEventLimit()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        for (var index = 0; index < 10; index++)
            journal.RecordSteering($"steering-{index}");
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.LatestAgentTaskEventJournal = journal.Snapshot();

        var report = CopilotAgentTaskEventDiagnostics.Format(conversation, "5");

        Assert.Contains("最近 5 / 11 条（新到旧）", report, StringComparison.Ordinal);
        Assert.Contains("另有 6 条较早事件未显示", report, StringComparison.Ordinal);
        Assert.Equal(5, report.Split(Environment.NewLine).Count(line => line.StartsWith('#')));
    }

    [Fact]
    public void ErrorsFilterShowsOnlyFailureEvidenceAndKeepsItRedacted()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordSteering("keep going");
        journal.Observe(CopilotAgentEvent.Error("Provider failed with api_key=runtime-secret."));
        journal.RecordApprovalDecision(
            "WriteWorkspace",
            "raw-call-id",
            "raw-approval-id",
            approved: false);
        journal.RecordBlocker(new CopilotAgentBlockerSnapshot
        {
            Code = "provider_error",
            Summary = "Blocked with token=blocker-secret.",
            ToolName = "InspectWorkspace",
        });
        journal.RecordStop(CopilotAgentStopReason.ProviderFailure);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Failures";
        conversation.LatestAgentTaskEventJournal = journal.Snapshot();

        var report = CopilotAgentTaskEventDiagnostics.Format(conversation, "errors");

        Assert.Contains("失败 4 / 4 条（新到旧）", report, StringComparison.Ordinal);
        Assert.Contains("RuntimeError", report, StringComparison.Ordinal);
        Assert.Contains("ApprovalDenied · WriteWorkspace", report, StringComparison.Ordinal);
        Assert.Contains("BlockerDetected · InspectWorkspace · provider_error", report, StringComparison.Ordinal);
        Assert.Contains("RunStopped · ProviderFailure", report, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStarted", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SteeringQueued", report, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("blocker-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-call-id", report, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-approval-id", report, StringComparison.Ordinal);
        Assert.Contains("<redacted>", report, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidTaskLogArgumentsReturnUsageWithoutReadingTheConversation()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Sensitive title";

        var report = CopilotAgentTaskEventDiagnostics.Format(conversation, "101");

        Assert.Equal(CopilotAgentTaskEventDiagnostics.Usage, report);
        Assert.DoesNotContain("Sensitive title", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorsFilterExplainsWhenNoFailuresWerePersisted()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Completed);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.LatestAgentTaskEventJournal = journal.Snapshot();

        var report = CopilotAgentTaskEventDiagnostics.Format(conversation, "errors");

        Assert.Contains("没有已保存的失败事件", report, StringComparison.Ordinal);
        Assert.Contains("不包含工具参数、模型隐藏推理或授权凭据", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatBoundsTheVisibleEventList()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        for (var index = 0; index < CopilotAgentTaskEventDiagnostics.MaximumDisplayedEvents + 5; index++)
            journal.RecordSteering($"steering-{index}");
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.LatestAgentTaskEventJournal = journal.Snapshot();

        var report = CopilotAgentTaskEventDiagnostics.Format(conversation);

        Assert.Contains("最近 20 / 26 条（新到旧）", report);
        Assert.Contains("另有 6 条较早事件未显示", report);
        Assert.Equal(
            CopilotAgentTaskEventDiagnostics.MaximumDisplayedEvents,
            report.Split(Environment.NewLine).Count(line => line.StartsWith('#')));
    }

    [Fact]
    public void LatestJournalPersistsWithoutAnExecutableCheckpoint()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Cancelled);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        Assert.True(conversation.UpdateLatestAgentTaskEventJournal(journal.Snapshot()));
        conversation.AgentSessionCheckpoint = null;

        var json = JsonConvert.SerializeObject(conversation);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(json);

        Assert.NotNull(restored);
        Assert.Null(restored.AgentSessionCheckpoint);
        Assert.NotNull(restored.LatestAgentTaskEventJournal);
        Assert.Equal(
            CopilotAgentStopReason.Cancelled.ToString(),
            restored.LatestAgentTaskEventJournal.Events[^1].State);
        Assert.False(restored.EnsureValid());
    }

    [Fact]
    public void ConversationMigratesJournalFromAnExistingCheckpoint()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var conversation = new CopilotConversationRecord
        {
            AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile",
                SerializedSessionJson = "{}",
                HookSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentHookSurfaceVersion,
                HookSurfaceFingerprint = new string('a', 64),
                TaskEventJournal = journal.Snapshot(),
            },
        };

        Assert.True(conversation.EnsureValid());
        Assert.Same(
            conversation.AgentSessionCheckpoint.TaskEventJournal,
            conversation.LatestAgentTaskEventJournal);
    }

    [Fact]
    public void NewRunCanReplaceAnOlderJournalAfterSequenceRestarts()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var older = CreateSingleEventJournal(DateTimeOffset.UtcNow.AddMinutes(-1));
        var newer = CreateSingleEventJournal(DateTimeOffset.UtcNow);

        Assert.True(conversation.UpdateLatestAgentTaskEventJournal(older));
        Assert.True(conversation.UpdateLatestAgentTaskEventJournal(newer));
        Assert.Same(newer, conversation.LatestAgentTaskEventJournal);
    }

    [Fact]
    public void EmptyJournalIsOmittedAndExplained()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        var json = JsonConvert.SerializeObject(conversation);
        var report = CopilotAgentTaskEventDiagnostics.Format(conversation);

        Assert.DoesNotContain(nameof(CopilotConversationRecord.LatestAgentTaskEventJournal), json);
        Assert.Contains("还没有已保存的 Agent 任务事件", report);
    }

    private static CopilotAgentTaskEventJournalSnapshot CreateSingleEventJournal(DateTimeOffset occurredAtUtc)
    {
        var runId = CopilotAgentTaskEventIds.CreateRunId();
        const long sequence = 1;
        return new CopilotAgentTaskEventJournalSnapshot
        {
            Events =
            [
                new CopilotAgentTaskEvent
                {
                    Sequence = sequence,
                    Id = CopilotAgentTaskEventIds.CreateEventId(
                        sequence,
                        runId,
                        CopilotAgentTaskEventType.RunStarted,
                        occurredAtUtc),
                    Type = CopilotAgentTaskEventType.RunStarted,
                    OccurredAtUtc = occurredAtUtc,
                    RunId = runId,
                    SubjectId = runId,
                    State = "running",
                    Summary = "Agent run started.",
                },
            ],
        };
    }
}
