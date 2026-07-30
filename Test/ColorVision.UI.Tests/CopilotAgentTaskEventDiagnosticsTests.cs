using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskEventDiagnosticsTests
{
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
