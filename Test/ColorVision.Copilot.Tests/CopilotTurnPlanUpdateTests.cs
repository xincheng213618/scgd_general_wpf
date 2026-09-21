using ColorVision.Copilot;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnPlanUpdateTests
{
    [Fact]
    public void PersistedAssistantTaskLedgerRoundTripsWithoutMutatingItsDefaultSnapshot()
    {
        var source = CreateAssistantWithTaskLedger();

        var restored = Assert.IsType<CopilotChatMessage>(
            JsonConvert.DeserializeObject<CopilotChatMessage>(JsonConvert.SerializeObject(source)));

        AssertRestoredTaskLedger(source, restored);
    }

    [Fact]
    public void StateStoreSavesAndReopensAssistantTaskLedger()
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(CopilotTurnPlanUpdateTests), Guid.NewGuid().ToString("N"));
        var store = new CopilotChatStateStore(root);
        var source = CreateAssistantWithTaskLedger();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(source);
        var state = new CopilotChatState
        {
            ActiveProfileId = "profile",
            ActiveConversationId = conversation.Id,
            Conversations = [conversation],
        };

        try
        {
            store.Save(state);
            var reopenedStore = new CopilotChatStateStore(root);
            var restored = reopenedStore.Load();

            Assert.Equal(CopilotChatStateLoadSource.Primary, reopenedStore.LastLoadStatus.Source);
            AssertRestoredTaskLedger(source, Assert.Single(Assert.Single(restored.Conversations).Messages));
        }
        finally
        {
            // Remove only the test's named state file; leave any diagnostic or recovery artifacts intact.
            File.Delete(store.StateFilePath);
        }
    }

    [Fact]
    public void ConversationBranchCopiesAssistantTaskLedgerAsAnIndependentSnapshot()
    {
        var source = CreateAssistantWithTaskLedger();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect and verify")
        {
            RequestMode = CopilotAgentMode.Auto,
        });
        conversation.Messages.Add(source);

        var branch = CopilotConversationBranchService.CreateBranch(conversation, source);

        Assert.Equal(2, branch.Messages.Count);
        AssertRestoredTaskLedger(source, branch.Messages[1]);
        Assert.NotEqual(source.Id, branch.Messages[1].Id);
        Assert.Equal(2, conversation.Messages.Count);
    }

    private static CopilotChatMessage CreateAssistantWithTaskLedger() => new(CopilotChatRole.Assistant, "Inspection complete; verification pending.")
    {
        RequestMode = CopilotAgentMode.Auto,
        AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "plan",
            ResumedFromCheckpoint = true,
            Items =
            [
                new() { Id = 3, Title = "Inspect", Description = "Read the current state.", IsComplete = true },
                new() { Id = 8, Title = "Verify", Description = "Check the result." },
            ],
        },
    };

    private static void AssertRestoredTaskLedger(CopilotChatMessage source, CopilotChatMessage restored)
    {
        var ledger = restored.AgentTaskLedger;
        Assert.NotSame(source.AgentTaskLedger, ledger);
        Assert.Equal("plan", ledger.Mode);
        Assert.True(ledger.ResumedFromCheckpoint);
        Assert.Equal(2, ledger.TotalCount);
        Assert.Equal(1, ledger.CompletedCount);
        Assert.Equal(1, ledger.RemainingCount);
        for (var index = 0; index < ledger.Items.Count; index++)
        {
            Assert.NotSame(source.AgentTaskLedger.Items[index], ledger.Items[index]);
            Assert.Equal(source.AgentTaskLedger.Items[index].Id, ledger.Items[index].Id);
            Assert.Equal(source.AgentTaskLedger.Items[index].Title, ledger.Items[index].Title);
            Assert.Equal(source.AgentTaskLedger.Items[index].Description, ledger.Items[index].Description);
            Assert.Equal(source.AgentTaskLedger.Items[index].IsComplete, ledger.Items[index].IsComplete);
        }
        Assert.Throws<InvalidOperationException>(() => ledger.Mode = "execute");
        Assert.Throws<InvalidOperationException>(() => ledger.Items[1].IsComplete = true);
    }

    [Fact]
    public void PublishedAgentSnapshotsOwnTheirTaskLedgerPayloads()
    {
        var sourceItem = new CopilotAgentTaskItem
        {
            Id = 1,
            Title = "Inspect runtime",
        };
        var sourceItems = new[] { sourceItem };
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items = sourceItems,
        };

        var checkpointEvent = CopilotAgentEvent.CheckpointUpdated(CreateCheckpoint(), ledger);
        var runResult = new CopilotAgentRunResult { TaskLedger = ledger };
        sourceItem.Title = "rewritten after publication";
        sourceItems[0] = new CopilotAgentTaskItem { Id = 2, Title = "replacement" };

        var eventLedger = Assert.IsType<CopilotAgentTaskLedgerSnapshot>(checkpointEvent.TaskLedger);
        Assert.Equal("Inspect runtime", Assert.Single(eventLedger.Items).Title);
        Assert.Equal("Inspect runtime", Assert.Single(runResult.TaskLedger.Items).Title);
        var eventItems = Assert.IsAssignableFrom<IList<CopilotAgentTaskItem>>(
            eventLedger.Items);
        var resultItems = Assert.IsAssignableFrom<IList<CopilotAgentTaskItem>>(
            runResult.TaskLedger.Items);
        Assert.Throws<NotSupportedException>(() => eventItems[0] = sourceItems[0]);
        Assert.Throws<NotSupportedException>(() => resultItems[0] = sourceItems[0]);
        Assert.Throws<InvalidOperationException>(() => eventLedger.Mode = "plan");
        Assert.Throws<InvalidOperationException>(() => eventLedger.Items[0].Title = "rewritten");
        Assert.Throws<InvalidOperationException>(() => runResult.TaskLedger.Items[0].IsComplete = true);
    }

    [Fact]
    public void AssistantMessageOwnsItsNormalizedTaskLedger()
    {
        var sourceItem = new CopilotAgentTaskItem
        {
            Id = 1,
            Title = "  Inspect runtime  ",
        };
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "unexpected",
            Items = [sourceItem],
        };
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            AgentTaskLedger = ledger,
        };

        sourceItem.Title = "rewritten after persistence";

        Assert.Equal("execute", assistant.AgentTaskLedger.Mode);
        Assert.Equal("Inspect runtime", Assert.Single(assistant.AgentTaskLedger.Items).Title);
        var persistedItems = Assert.IsAssignableFrom<IList<CopilotAgentTaskItem>>(
            assistant.AgentTaskLedger.Items);
        Assert.Throws<NotSupportedException>(() =>
            persistedItems[0] = new CopilotAgentTaskItem { Id = 2, Title = "replacement" });
        Assert.Throws<InvalidOperationException>(() => assistant.AgentTaskLedger.Mode = "plan");
        Assert.Throws<InvalidOperationException>(() =>
            assistant.AgentTaskLedger.Items[0].Title = "rewritten through projection");
    }

    [Fact]
    public void AccumulatorEmitsOnlyChangedCheckpointPlansAndOwnsItsSnapshot()
    {
        var sourceItem = new CopilotAgentTaskItem
        {
            Id = 1,
            Title = "Inspect runtime",
            Description = "Read the current event path.",
        };
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items = [sourceItem],
        };
        var accumulator = new CopilotTurnPlanAccumulator();
        var checkpoint = CreateCheckpoint();

        Assert.True(accumulator.Observe(
            CopilotAgentEvent.CheckpointUpdated(checkpoint, ledger),
            out var first));
        Assert.False(accumulator.Observe(
            CopilotAgentEvent.CheckpointUpdated(checkpoint, new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem
                    {
                        Id = 1,
                        Title = "Inspect runtime",
                        Description = "Read the current event path.",
                    },
                ],
            }),
            out _));

        sourceItem.Title = "mutated after publication";
        sourceItem.IsComplete = true;
        Assert.Equal("Inspect runtime", first.Items[0].Step);
        Assert.Equal(CopilotTurnPlanItemStatus.Pending, first.Items[0].Status);

        Assert.True(accumulator.Observe(ledger, out var completed));
        Assert.Equal("mutated after publication", completed.Items[0].Step);
        Assert.Equal(CopilotTurnPlanItemStatus.Completed, completed.Items[0].Status);
    }

    [Fact]
    public void TaskLedgerNormalizationBoundsUntrustedPersistedPlans()
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "unexpected",
            Items = Enumerable.Range(0, CopilotAgentTaskLedgerSnapshot.MaxItems + 5)
                .Select(index => new CopilotAgentTaskItem
                {
                    Id = index,
                    Title = "  " + new string('t', CopilotAgentTaskItem.MaxTitleLength + 20) + "  ",
                    Description = "  " + new string('d', CopilotAgentTaskItem.MaxDescriptionLength + 20) + "  ",
                })
                .ToArray(),
        };

        Assert.True(ledger.EnsureValid());

        Assert.Equal("execute", ledger.Mode);
        Assert.Equal(CopilotAgentTaskLedgerSnapshot.MaxItems, ledger.Items.Count);
        Assert.All(ledger.Items, item => Assert.Equal(CopilotAgentTaskItem.MaxTitleLength, item.Title.Length));
        Assert.All(ledger.Items, item => Assert.Equal(CopilotAgentTaskItem.MaxDescriptionLength, item.Description.Length));
    }

    [Fact]
    public void TaskLedgerNormalizationLetsAValidTaskReplaceAnEarlierInvalidDuplicate()
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Items =
            [
                new CopilotAgentTaskItem { Id = 7, Title = "  " },
                new CopilotAgentTaskItem { Id = 7, Title = "Keep this task" },
            ],
        };

        Assert.True(ledger.EnsureValid());

        var task = Assert.Single(ledger.Items);
        Assert.Equal(7, task.Id);
        Assert.Equal("Keep this task", task.Title);
    }

    [Fact]
    public void ReducerRequiresFinalPlanSnapshotBeforeAgentTurnCompletion()
    {
        var result = CreateAgentResult(new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items = [new CopilotAgentTaskItem { Id = 1, Title = "Verify", IsComplete = true }],
        });
        var state = CopilotTurnEventReducer.Reduce(
            CreateStartedState(CopilotAgentMode.Auto),
            new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(state, new CopilotTurnCompletedEvent(result)));

        Assert.Contains("before its final plan update", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerAcceptsMatchingFinalPlanAfterAgentCompletedItem()
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items = [new CopilotAgentTaskItem { Id = 1, Title = "Verify", IsComplete = true }],
        };
        var result = CreateAgentResult(ledger);
        var state = CopilotTurnEventReducer.Reduce(
            CreateStartedState(CopilotAgentMode.Auto),
            new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(ledger)));
        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnCompletedEvent(result));

        Assert.NotNull(state.Plan);
        Assert.Same(result, CopilotTurnEventReducer.RequireCompletion(state));
    }

    [Fact]
    public void ReducerRejectsDuplicatePlanSnapshots()
    {
        var snapshot = CopilotTurnPlanSnapshot.FromTaskLedger(new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "plan",
            Items = [new CopilotAgentTaskItem { Id = 1, Title = "Design" }],
        });
        var state = CopilotTurnEventReducer.Reduce(
            CreateStartedState(CopilotAgentMode.Plan),
            new CopilotTurnPlanUpdatedEvent(snapshot));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(snapshot.ToTaskLedgerSnapshot()))));

        Assert.Contains("duplicate plan snapshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PresenterAppliesPlanSnapshotAsImmediatePersistentState()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var snapshot = new CopilotTurnPlanSnapshot(
            "execute",
            false,
            [
                new CopilotTurnPlanItemSnapshot(1, "Inspect", string.Empty, CopilotTurnPlanItemStatus.Completed),
                new CopilotTurnPlanItemSnapshot(2, "Verify", "Run focused tests.", CopilotTurnPlanItemStatus.Pending),
            ]);

        var presentation = CopilotAssistantMessagePresenter.ApplyAgentEvent(
            assistant,
            CopilotAgentEvent.PlanUpdated(snapshot));

        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, presentation.PersistenceMode);
        Assert.Equal(2, assistant.AgentTaskLedger.TotalCount);
        Assert.Equal(1, assistant.AgentTaskLedger.CompletedCount);
        Assert.Equal("Verify", assistant.AgentTaskLedger.Items[1].Title);
    }

    [Fact]
    public void EventSinkPublishesTypedPlanSnapshot()
    {
        var emitted = new List<CopilotTurnEvent>();
        var sink = new CopilotTurnEventSink(emitted.Add);
        var snapshot = CopilotTurnPlanSnapshot.FromTaskLedger(new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "plan",
            Items = [new CopilotAgentTaskItem { Id = 1, Title = "Plan" }],
        });

        sink.OnPlanUpdated(snapshot);

        var planEvent = Assert.IsType<CopilotTurnPlanUpdatedEvent>(Assert.Single(emitted));
        Assert.Same(snapshot, planEvent.Snapshot);
    }

    private static CopilotTurnResult CreateAgentResult(CopilotAgentTaskLedgerSnapshot ledger) =>
        CopilotTurnResult.FromAgent(
            CopilotAgentMode.Auto,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult { TaskLedger = ledger });

    private static CopilotTurnEventState CreateStartedState(CopilotAgentMode mode) =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(mode),
            new CopilotTurnStartedEvent(mode));

    private static CopilotAgentSessionCheckpoint CreateCheckpoint() => new()
    {
        ProfileKey = "test-profile",
        SerializedSessionJson = "{}",
    };
}
