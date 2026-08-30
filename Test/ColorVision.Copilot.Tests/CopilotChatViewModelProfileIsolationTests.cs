using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CopilotChatViewModelProfileIsolationFixture
{
    public const string Name = "CopilotChatViewModel profile isolation";
}

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotChatViewModelProfileIsolationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void ClipboardImageCanCreateMultipleMissingStorageDirectories()
    {
        RunAttachmentTestOnSta(() =>
        {
            var root = Directory.CreateTempSubdirectory("CopilotClipboardStorage-").FullName;
            var storePath = Path.Combine(root, "new-parent", "nested", "attachments");
            var profile = CreateProfile("clipboard", "Clipboard", "test-model");
            var conversation = CreateConversation(profile, "clipboard-conversation", "preserved draft");
            var state = new CopilotChatState { ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation] };
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "test-token", Profiles = [profile] };
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            using var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state, storePath), config, new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
            try
            {
                var image = BitmapSource.Create(4, 3, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 3 * 4], 4 * 4);
                image.Freeze();
                var saveMethod = typeof(CopilotChatViewModel).GetMethod("SaveClipboardImage", BindingFlags.Instance | BindingFlags.NonPublic)!;

                var savedPath = Assert.IsType<string>(saveMethod.Invoke(viewModel, [image, CancellationToken.None]));

                Assert.Equal(storePath, Path.GetDirectoryName(savedPath));
                Assert.Equal(savedPath, Assert.Single(Directory.GetFiles(storePath, "clipboard-*.png")));
                var savedImage = BitmapFrame.Create(new Uri(savedPath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.Equal(4, savedImage.PixelWidth);
                Assert.Equal(3, savedImage.PixelHeight);
                Assert.Equal("preserved draft", conversation.DraftText);
            }
            finally
            {
                viewModel.Dispose();
                Directory.Delete(root, recursive: true);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClipboardImageCannotBeStoredThroughLinkedDirectory(bool linkedAncestor)
    {
        RunAttachmentTestOnSta(() =>
        {
            var root = Directory.CreateTempSubdirectory("CopilotClipboardStorage-").FullName;
            var outside = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
            var linkedPath = Path.Combine(root, "linked");
            var storePath = linkedAncestor ? Path.Combine(linkedPath, "new-store") : linkedPath;
            Directory.CreateSymbolicLink(linkedPath, outside);
            var profile = CreateProfile("clipboard", "Clipboard", "test-model");
            var conversation = CreateConversation(profile, "clipboard-conversation", "preserved draft");
            var state = new CopilotChatState { ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation] };
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "test-token", Profiles = [profile] };
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            using var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state, storePath), config, new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
            try
            {
                var image = BitmapSource.Create(4, 3, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 3 * 4], 4 * 4);
                image.Freeze();
                var saveMethod = typeof(CopilotChatViewModel).GetMethod("SaveClipboardImage", BindingFlags.Instance | BindingFlags.NonPublic)!;

                var error = Assert.Throws<TargetInvocationException>(() =>
                    saveMethod.Invoke(viewModel, [image, CancellationToken.None]));

                Assert.IsType<InvalidOperationException>(error.InnerException);
                Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
                Assert.Empty(conversation.Attachments);
                Assert.Equal("preserved draft", conversation.DraftText);
            }
            finally
            {
                viewModel.Dispose();
                Directory.Delete(linkedPath, recursive: false);
                Directory.Delete(root, recursive: true);
            }
        });
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("complete")]
    public void ClipboardImageCompletedBeforeUiContinuationIsKeptOnlyWhenAttached(string transition)
    {
        RunAttachmentTestOnSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "CopilotClipboardLifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var profile = CreateProfile("clipboard", "Clipboard", "test-model");
            var conversation = CreateConversation(profile, "clipboard-conversation", "preserved draft");
            var existingFile = Path.Combine(root, "existing.png");
            File.WriteAllBytes(existingFile, [1, 2, 3]);
            var existingAttachment = CopilotAttachmentItem.CreateImage(existingFile);
            conversation.Attachments.Add(existingAttachment);
            var state = new CopilotChatState { ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation] };
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "test-token", Profiles = [profile] };
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            using var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state, root), config, new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
            using var context = new PausedAttachmentSynchronizationContext();
            var previousContext = SynchronizationContext.Current;
            try
            {
                var pixels = new byte[512 * 512 * 4];
                new Random(42).NextBytes(pixels);
                var image = BitmapSource.Create(512, 512, 96, 96, PixelFormats.Bgra32, null, pixels, 512 * 4);
                image.Freeze();
                var saveMethod = typeof(CopilotChatViewModel).GetMethod("SaveClipboardImageAttachmentAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                SynchronizationContext.SetSynchronizationContext(context);
                var operation = Assert.IsType<Task<bool>>(saveMethod.Invoke(viewModel, [image]), exactMatch: false);
                Assert.True(context.CallbackPosted.Wait(TestTimeout), "The clipboard save continuation was not queued.");
                Assert.False(operation.IsCompleted);
                var savedFile = Assert.Single(Directory.GetFiles(root, "clipboard-*.png"));
                Assert.Same(existingAttachment, Assert.Single(conversation.Attachments));

                if (transition == "cancel")
                    viewModel.PrimaryActionCommand.Execute(null);
                else if (transition == "dispose")
                    viewModel.Dispose();

                context.RunPending();
                Assert.True(operation.IsCompleted);
                Assert.Equal(transition == "complete", operation.GetAwaiter().GetResult());
                Assert.Equal(transition == "complete", File.Exists(savedFile));
                Assert.True(File.Exists(existingFile));
                Assert.Equal("preserved draft", conversation.DraftText);
                Assert.False(viewModel.IsBusy);
                if (transition == "complete")
                {
                    Assert.Equal(2, conversation.Attachments.Count);
                    Assert.Contains(conversation.Attachments, attachment => attachment.Value == savedFile);
                }
                else
                {
                    Assert.Same(existingAttachment, Assert.Single(conversation.Attachments));
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
                viewModel.Dispose();
                Directory.Delete(root, recursive: true);
            }
        });
    }

    [Fact]
    public async Task BackgroundTurnCompletionDoesNotApplyProfileFromNewlySelectedConversation()
    {
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "profile-isolation-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig>
            {
                profileA,
                profileB,
            },
        };
        var conversationA = CopilotConversationRecord.CreateEmpty(profileA.Id, profileA.DisplayLabel);
        conversationA.Id = "conversation-a";
        conversationA.DraftRequestMode = CopilotAgentMode.Explain;
        var conversationB = CopilotConversationRecord.CreateEmpty(profileB.Id, profileB.DisplayLabel);
        conversationB.Id = "conversation-b";
        var state = new CopilotChatState
        {
            ActiveConversationId = conversationA.Id,
            ActiveProfileId = profileA.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                conversationA,
                conversationB,
            },
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            runtime,
            taskHost);
        CopilotHostedAgentRun? activeRun = null;
        try
        {
            Assert.Same(conversationA, viewModel.SelectedConversation);
            Assert.Same(profileA, viewModel.SelectedProfile);

            viewModel.InputText = "Explain conversation A.";
            viewModel.SendCommand.Execute(null);

            var request = await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(conversationA.Id, request.ConversationId);
            Assert.Equal(profileA.Id, request.Profile.Id);
            Assert.True(activeRun.IsAgent);
            Assert.True(viewModel.CanSwitchConversation);
            Assert.True(viewModel.SelectConversationCommand.CanExecute(conversationB));

            viewModel.SelectConversationCommand.Execute(conversationB);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);
            Assert.Equal(profileA.Id, conversationA.ProfileId);
            Assert.Equal(profileA.DisplayLabel, conversationA.ProfileDisplayName);
            Assert.Equal(profileB.Id, conversationB.ProfileId);
            Assert.Equal(profileB.DisplayLabel, conversationB.ProfileDisplayName);
        }
        finally
        {
            runtime.Release();
            var pendingRun = activeRun ?? taskHost.ActiveRun;
            if (pendingRun != null && !pendingRun.Completion.IsCompleted)
            {
                try
                {
                    await pendingRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            viewModel.Dispose();
        }
    }

    [Fact]
    public void DeliveredSteeringRecoveryRecordIsCommittedWithTheNextCheckpoint()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "steering-checkpoint-commit-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var steeringMessage = new CopilotSteeringMessageSnapshot(
            "steering:checkpoint-commit",
            "keep the current constraint");

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:checkpoint-commit");
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                hostedRun.Id,
                steeringMessage,
                DateTimeOffset.UtcNow));
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.SteeringDelivered([steeringMessage]));

            Assert.Single(conversation.PendingSteeringRecoveries);

            var taskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem
                    {
                        Id = 1,
                        Title = "Persist steering",
                        Description = "Commit delivered steering with the checkpoint.",
                    },
                ],
            };
            var staleCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(staleCheckpoint, taskLedger));

            Assert.Single(conversation.PendingSteeringRecoveries);

            var checkpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                ConversationMemory =
                [
                    new CopilotRequestMessage("user", steeringMessage.Text)
                    {
                        IsSteering = true,
                    },
                ],
                UpdatedAtUtc = staleCheckpoint.UpdatedAtUtc.AddSeconds(1),
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(checkpoint, taskLedger));

            Assert.Empty(conversation.PendingSteeringRecoveries);
            var committedCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
                conversation.AgentSessionCheckpoint);
            Assert.NotSame(checkpoint, committedCheckpoint);
            Assert.Equal(checkpoint.UpdatedAtUtc, committedCheckpoint.UpdatedAtUtc);
            Assert.Equal(checkpoint.ConversationMemory, committedCheckpoint.ConversationMemory);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public void RejectedCheckpointCannotCommitDeliveredSteeringRecovery()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "rejected-steering-checkpoint-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var steeringMessage = new CopilotSteeringMessageSnapshot(
            "steering:rejected-checkpoint",
            "retain this instruction until durable");
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var regressingJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var currentCheckpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "profile-a|model-a",
            SerializedSessionJson = "{}",
            TaskEventJournal = journal.Snapshot(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        Assert.True(conversation.SetAgentSessionCheckpoint(currentCheckpoint));
        var acceptedCheckpoint = conversation.AgentSessionCheckpoint;

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:rejected-checkpoint");
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                hostedRun.Id,
                steeringMessage,
                DateTimeOffset.UtcNow));
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.SteeringDelivered([steeringMessage]));

            var rejectedCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                ConversationMemory =
                [
                    new CopilotRequestMessage("user", steeringMessage.Text)
                    {
                        IsSteering = true,
                    },
                ],
                TaskEventJournal = regressingJournal,
                UpdatedAtUtc = currentCheckpoint.UpdatedAtUtc.AddSeconds(1),
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(
                    rejectedCheckpoint,
                    new CopilotAgentTaskLedgerSnapshot
                    {
                        Mode = "execute",
                        Items =
                        [
                            new CopilotAgentTaskItem
                            {
                                Id = 1,
                                Title = "Retain steering",
                                Description = "Do not commit recovery through rejected evidence.",
                            },
                        ],
                    }));

            Assert.Same(acceptedCheckpoint, conversation.AgentSessionCheckpoint);
            Assert.Single(conversation.PendingSteeringRecoveries);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public void RejectedTerminalCheckpointRestoresDeliveredSteeringToDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "rejected-terminal-checkpoint-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var steeringMessage = new CopilotSteeringMessageSnapshot(
            "steering:rejected-terminal-checkpoint",
            "restore this terminal instruction");
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var rejectedJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var currentCheckpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "profile-a|model-a",
            SerializedSessionJson = "{}",
            TaskEventJournal = journal.Snapshot(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        conversation.SetAgentSessionCheckpoint(currentCheckpoint);
        var acceptedCheckpoint = conversation.AgentSessionCheckpoint;

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:rejected-terminal-checkpoint");
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                hostedRun.Id,
                steeringMessage,
                DateTimeOffset.UtcNow));
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.SteeringDelivered([steeringMessage]));
            var rejectedCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                ConversationMemory =
                [
                    new CopilotRequestMessage("user", steeringMessage.Text)
                    {
                        IsSteering = true,
                    },
                ],
                TaskEventJournal = rejectedJournal,
                UpdatedAtUtc = currentCheckpoint.UpdatedAtUtc.AddSeconds(1),
            };

            var accepted = CommitAgentRunStateAndResolveSteering(
                viewModel,
                hostedRun,
                conversation,
                rejectedJournal,
                rejectedCheckpoint,
                CopilotAgentStopReason.Paused);

            Assert.False(accepted);
            Assert.Same(acceptedCheckpoint, conversation.AgentSessionCheckpoint);
            Assert.Empty(conversation.PendingSteeringRecoveries);
            Assert.Contains(steeringMessage.Text, conversation.DraftText, StringComparison.Ordinal);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task PausedBoundGoalStillAccountsTheCompletedTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "paused-goal-accounting-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal.WithState(
            CopilotConversationGoalState.Paused,
            createdAt.AddMinutes(1),
            "用户暂停");
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:paused-goal-accounting");
        try
        {
            await ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(turnUsage.EffectiveTotalTokens, conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task PausingDuringGoalEvaluationAccountsEvaluationUsageWithoutApplyingItsVerdict()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "paused-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluationUsage = new CopilotTokenUsage(3, 2, 5);
        var evaluator = new GatedGoalCompletionEvaluator(evaluationUsage);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:paused-goal-evaluation");
        try
        {
            var processing = ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);
            await evaluator.Entered.WaitAsync(TestTimeout);
            conversation.Goal = activeGoal.WithState(
                CopilotConversationGoalState.Paused,
                createdAt.AddMinutes(1),
                "用户暂停");
            evaluator.Release();
            await processing;

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(
                turnUsage.Add(evaluationUsage).EffectiveTotalTokens,
                conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
        }
        finally
        {
            evaluator.Release();
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellingGoalEvaluationAfterCompletedTurnPreservesAccountingAndPausesGoal()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluator = new GatedGoalCompletionEvaluator(CopilotTokenUsage.Empty);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:cancelled-goal-evaluation");
        try
        {
            var processing = ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);
            await evaluator.Entered.WaitAsync(TestTimeout);

            Assert.True(hostedRun.TryRequestCancel());
            await processing;

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(turnUsage.EffectiveTotalTokens, conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
            Assert.Equal(CopilotAgentStopReason.Completed, assistantMessage.AgentStopReason);
            Assert.False(assistantMessage.WasResponseInterrupted);
        }
        finally
        {
            evaluator.Release();
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellationObservedWithEvaluationResultRetainsItsUsageWithoutApplyingVerdict()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "late-cancelled-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluationUsage = new CopilotTokenUsage(3, 2, 5);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:late-cancelled-goal-evaluation");
        var evaluator = new CancellingGoalCompletionEvaluator(
            () => Assert.True(hostedRun.TryRequestCancel()),
            evaluationUsage);
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        try
        {
            await ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(
                turnUsage.Add(evaluationUsage).EffectiveTotalTokens,
                conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
            Assert.Equal(CopilotAgentStopReason.Completed, assistantMessage.AgentStopReason);
            Assert.False(assistantMessage.WasResponseInterrupted);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellationBeforeGoalContinuationCommitPausesGoalAndRemovesAutomaticWork()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-goal-continuation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var continuedGoal = CopilotConversationGoal.Create(
                "Finish the verified runtime slice",
                createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                turnUsage,
                elapsedSeconds: 2,
                evaluated: true,
                continued: true,
                "More verified work remains.",
                createdAt.AddMinutes(1));
        conversation.Goal = continuedGoal;
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var taskHost = new CopilotAgentTaskHost();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        conversation.Goal = continuedGoal;
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostedRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            var completedTurnSnapshot = new CopilotAgentHostContextSnapshot("", "", []);
            var completedTurnRuntimeConfig = new CopilotTurnRuntimeConfigSnapshot(
                new CopilotAgentDefaultsConfig(),
                []);
            Assert.True(TryQueueGoalContinuation(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                completedTurnRuntimeConfig,
                continuedGoal.Id,
                "More verified work remains."));
            Assert.Single(taskHost.QueuedRuns);

            Assert.True(taskHost.RequestCancel(hostedRun.Id));

            var queued = TryQueueGoalContinuation(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                completedTurnRuntimeConfig,
                continuedGoal.Id,
                "More verified work remains.");

            Assert.False(queued);
            Assert.Empty(taskHost.QueuedRuns);
            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(continuedGoal.TurnCount, conversation.Goal.TurnCount);
            Assert.Equal(continuedGoal.EvaluationCount, conversation.Goal.EvaluationCount);
            Assert.Equal(continuedGoal.TokensUsed, conversation.Goal.TokensUsed);
            Assert.Equal(continuedGoal.IterationLog.Count, conversation.Goal.IterationLog.Count);
            Assert.Contains(
                "continuation queueing cancelled",
                assistantMessage.ExecutionContent,
                StringComparison.Ordinal);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            try
            {
                await hostedRun.Completion.WaitAsync(TestTimeout);
            }
            catch (OperationCanceledException)
            {
            }
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task UserQuestionAnswerStateChangesOnlyWhenTheRuntimeEventArrives()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "question-answer-event-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var runtime = new AcceptingQuestionTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var releaseRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var hostedRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Plan,
            async _ => await releaseRun.Task);
        var question = new CopilotUserQuestionSnapshot
        {
            RequestId = "question:" + new string('a', 32),
            ConversationId = conversation.Id,
            TaskId = hostedRun.Id,
            Header = "Scope",
            Question = "Which scope should be used?",
            Options =
            [
                new CopilotUserQuestionOption
                {
                    RequestId = "question:" + new string('a', 32),
                    TaskId = hostedRun.Id,
                    Label = "Current (Recommended)",
                    Description = "Keep the current bounded scope.",
                },
                new CopilotUserQuestionOption
                {
                    RequestId = "question:" + new string('a', 32),
                    TaskId = hostedRun.Id,
                    Label = "Expand",
                    Description = "Include adjacent modules.",
                },
            ],
            RequestedAtUtc = DateTimeOffset.UtcNow,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            UserQuestion = question,
        };
        conversation.Messages.Add(assistantMessage);
        try
        {
            viewModel.InputText = "Keep the current scope";

            Assert.True(viewModel.SubmitUserQuestionAnswerCommand.CanExecute(null));
            viewModel.SubmitUserQuestionAnswerCommand.Execute(null);

            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.Equal("Keep the current scope", runtime.Answer);
            var persistedQuestion = Assert.IsType<CopilotUserQuestionSnapshot>(assistantMessage.UserQuestion);
            Assert.NotSame(question, persistedQuestion);
            Assert.Equal(question.RequestId, persistedQuestion.RequestId);
            Assert.True(persistedQuestion.IsPending);

            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.UserQuestionResolved(
                    question.Resolve(
                        CopilotUserQuestionResolution.Answered,
                        "Keep the current scope")));

            Assert.Equal(
                CopilotUserQuestionResolution.Answered,
                assistantMessage.UserQuestion.Resolution);
            Assert.Equal("Keep the current scope", assistantMessage.UserQuestion.Answer);
        }
        finally
        {
            releaseRun.TrySetResult();
            await hostedRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task EditingGoalDuringActiveTurnQueuesOnlyTheLatestGoalAsNextWork()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "running-goal-edit-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var originalGoal = CopilotConversationGoal.Create(
            "Finish the original runtime objective",
            new DateTimeOffset(2026, 8, 11, 13, 0, 0, TimeSpan.Zero));
        conversation.Goal = originalGoal;
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);

            Assert.True(viewModel.EditConversationGoalCommand.CanExecute(null));

            viewModel.InputText = "/goal edit Finish the revised runtime objective";
            viewModel.SendCommand.Execute(null);

            var firstEditedGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.NotEqual(originalGoal.Id, firstEditedGoal.Id);
            Assert.Equal("Finish the revised runtime objective", firstEditedGoal.Objective);
            var firstQueued = Assert.Single(viewModel.QueuedFollowUps);
            Assert.Equal(firstEditedGoal.Id, firstQueued.GoalId);
            Assert.Equal(firstEditedGoal.Objective, firstQueued.Prompt);
            Assert.True(firstQueued.IsGoalBound);
            Assert.False(firstQueued.IsAutomaticGoalContinuation);

            viewModel.InputText = "/goal edit Finish the latest runtime objective";
            viewModel.SendCommand.Execute(null);

            var latestGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.NotEqual(firstEditedGoal.Id, latestGoal.Id);
            Assert.Equal("Finish the latest runtime objective", latestGoal.Objective);
            var latestQueued = Assert.Single(viewModel.QueuedFollowUps);
            Assert.NotEqual(firstQueued.RunId, latestQueued.RunId);
            Assert.Equal(latestGoal.Id, latestQueued.GoalId);
            Assert.Equal(latestGoal.Objective, latestQueued.Prompt);
            Assert.True(latestQueued.IsGoalBound);
            Assert.False(latestQueued.IsAutomaticGoalContinuation);
            Assert.Single(taskHost.QueuedRuns);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task EditingGoalDuringActiveTurnPausesItWhenTheFollowUpQueueIsFull()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "running-goal-edit-full-queue-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Goal = CopilotConversationGoal.Create(
            "Finish the original runtime objective",
            new DateTimeOffset(2026, 8, 11, 13, 30, 0, TimeSpan.Zero));
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            for (var index = 0; index < taskHost.MaxQueuedRuns; index++)
            {
                Assert.True(taskHost.TryScheduleFollowUp(
                    conversation.Id,
                    CopilotAgentMode.Auto,
                    static _ => Task.CompletedTask,
                    out _,
                    out _));
            }

            viewModel.InputText = "/goal edit Finish the revised runtime objective";
            viewModel.SendCommand.Execute(null);

            var editedGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.Equal("Finish the revised runtime objective", editedGoal.Objective);
            Assert.Equal(CopilotConversationGoalState.Paused, editedGoal.State);
            Assert.Empty(viewModel.QueuedFollowUps);
            Assert.Contains("队列已满", viewModel.LocalCommandResultText, StringComparison.Ordinal);
            Assert.Contains("目标已暂停", viewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task SupersededExplicitGoalStartExitsWithoutRunningAgainstTheNewGoal()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "superseded-goal-start-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Goal = CopilotConversationGoal.Create(
            "Finish the latest runtime objective",
            new DateTimeOffset(2026, 8, 11, 14, 0, 0, TimeSpan.Zero));
        var runtime = new GatedFailingTurnRuntime();
        var queuedFollowUp = new CopilotQueuedFollowUp(
            "superseded-goal-run",
            conversation.Id,
            conversation.Title,
            "Finish the superseded runtime objective",
            CopilotAgentMode.Auto,
            profile,
            new CopilotAgentHostContextSnapshot("", "", []),
            goalId: "superseded-goal-id",
            automaticGoalContinuation: false);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            runtime,
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto);
        var executeMethod = typeof(CopilotChatViewModel).GetMethod(
            "ExecuteQueuedFollowUpAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExecuteQueuedFollowUpAsync was not found.");

        var execution = Assert.IsAssignableFrom<Task>(executeMethod.Invoke(
            viewModel,
            [hostedRun, queuedFollowUp]));
        await execution.WaitAsync(TestTimeout);

        Assert.False(runtime.Entered.IsCompleted);
        Assert.Equal("Finish the latest runtime objective", conversation.Goal.Objective);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void PromptHistorySearchUsesAnOverlayAndRestoresTheCompleteComposerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "composer-search-overlay-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var skillReference = new CopilotAgentSkillReference
        {
            Name = "sample-skill",
            SkillFilePath = Path.GetFullPath(
                Path.Combine("skills", "sample-skill", "SKILL.md")),
        };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = "conversation-a";
        conversation.DraftText = "$sample-skill current draft";
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        conversation.DraftAgentSkillReference = skillReference;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "older prompt"));
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        try
        {
            Assert.True(viewModel.TryOpenPromptHistorySearch());

            viewModel.InputText = "older";

            Assert.Equal("older", viewModel.InputText);
            Assert.Equal("$sample-skill current draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.Equal(
                CopilotWorkspaceReviewTarget.WorkingTree,
                conversation.DraftWorkspaceReviewTarget?.Target);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);

            viewModel.DismissPromptHistorySearch();

            Assert.Equal("$sample-skill current draft", viewModel.InputText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task ScheduledTurnConsumesCapturedAttachmentsButPreservesAttachmentsAddedDuringScheduling()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-attachment-race-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        var lateAttachment = CopilotAttachmentItem.CreateContext("late context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    conversation.Attachments.Add(lateAttachment);
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.DoesNotContain(capturedAttachment, conversation.Attachments);
            Assert.Contains(lateAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    [Fact]
    public async Task NewerComposerEditDuringSchedulingIsNotClearedByTheCapturedTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-stale-token-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    viewModel.InputText = "newer draft";
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Contains(capturedAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    [Fact]
    public async Task ActiveComposerSuggestsSlashCommandsThatCanRunFromTheNextTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-command-completion-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/pl";

            Assert.Contains(
                viewModel.LocalCommandSuggestions,
                command => command.Name == "/plan");
            Assert.Contains("排到下一轮", viewModel.LocalCommandSuggestionHeader, StringComparison.Ordinal);
        }
        finally
        {
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task QueueActionDefersSlashCommandAndPreservesNewerComposerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-status-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = CopilotAttachmentItem.CreateContext("queued command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(queuedAttachment);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, new GatedFailingTurnRuntime(), taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/status";

            Assert.True(viewModel.QueueFollowUpCommand.CanExecute(null));
            viewModel.QueueFollowUpCommand.Execute(null);

            var queuedItem = Assert.Single(viewModel.QueuedFollowUps);
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.True(queuedItem.IsLocalCommand);
            Assert.False(queuedRun.IsAgent);
            Assert.Equal(string.Empty, viewModel.LocalCommandResultTitle);
            Assert.DoesNotContain(queuedAttachment, conversation.Attachments);
            Assert.Empty(conversation.Messages);

            viewModel.InputText = "newer draft";
            conversation.Attachments.Add(newerAttachment);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await queuedRun.Completion.WaitAsync(TestTimeout);

            Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Contains(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.Empty(conversation.Messages);
            Assert.Empty(viewModel.QueuedFollowUps);
        }
        finally
        {
            releaseActive.TrySetResult();
            viewModel.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingQueuedCommandBeforeDispatchPreservesGoalAndRestoresComposer(bool hasNewerDraft)
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-queued-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = CopilotAttachmentItem.CreateContext("queued command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(queuedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var originalGoal = CopilotConversationGoal.Create("Keep the active goal", DateTimeOffset.UtcNow);
        conversation.Goal = originalGoal;
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? cancelOnStart = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/goal clear";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.True(Assert.Single(viewModel.QueuedFollowUps).IsLocalCommand);
            Assert.DoesNotContain(queuedAttachment, conversation.Attachments);
            var cancellationAccepted = false;
            cancelOnStart = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started && args.Run.Id == queuedRun.Id)
                    cancellationAccepted = taskHost.RequestCancel(queuedRun.Id);
            };
            taskHost.Changed += cancelOnStart;
            if (hasNewerDraft)
            {
                viewModel.InputText = "newer draft";
                conversation.Attachments.Add(newerAttachment);
            }

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await queuedRun.Completion.WaitAsync(TestTimeout));

            var expectedDraft = hasNewerDraft
                ? "newer draft" + Environment.NewLine + Environment.NewLine + "/goal clear"
                : "/goal clear";
            Assert.True(cancellationAccepted);
            Assert.Same(originalGoal, conversation.Goal);
            Assert.Equal(expectedDraft, conversation.DraftText);
            Assert.Equal(expectedDraft, viewModel.InputText);
            Assert.Contains(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            if (hasNewerDraft)
                Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.Equal(string.Empty, viewModel.LocalCommandResultTitle);
            Assert.False(runtime.Entered.IsCompleted);
            Assert.Empty(conversation.Messages);
            Assert.Empty(viewModel.QueuedFollowUps);
        }
        finally
        {
            taskHost.Changed -= cancelOnStart;
            releaseActive.TrySetResult();
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task QueuedCommandPersistenceFailurePreservesGoalAndRestoresComposer()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "failed-queued-command-save-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = CopilotAttachmentItem.CreateContext("queued command attachment");
        conversation.Attachments.Add(queuedAttachment);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        var failSaves = 0;
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateStore = new InMemoryStateStore(state, saveSerializedAsync: async cancellationToken =>
        {
            if (Volatile.Read(ref failSaves) == 0)
                return;
            saveEntered.TrySetResult();
            await releaseSave.Task.WaitAsync(cancellationToken);
            throw new IOException("Expected queued command persistence failure.");
        });
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(new CopilotChatService(), stateStore, config, runtime, taskHost);
        var originalGoal = CopilotConversationGoal.Create("Keep the active goal", DateTimeOffset.UtcNow);
        conversation.Goal = originalGoal;
        var activeRun = taskHost.Start(conversation.Id, CopilotAgentMode.Auto, _ => releaseActive.Task);
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? failSaveOnStart = null;

        try
        {
            viewModel.InputText = "/goal clear";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            failSaveOnStart = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started && args.Run.Id == queuedRun.Id)
                    Volatile.Write(ref failSaves, 1);
            };
            taskHost.Changed += failSaveOnStart;
            viewModel.InputText = "newer draft";

            releaseActive.TrySetResult();
            await saveEntered.Task.WaitAsync(TestTimeout);
            Assert.Same(originalGoal, conversation.Goal);
            releaseSave.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await Assert.ThrowsAsync<IOException>(async () =>
                await queuedRun.Completion.WaitAsync(TestTimeout));

            var expectedDraft = "newer draft" + Environment.NewLine + Environment.NewLine + "/goal clear";
            Assert.Same(originalGoal, conversation.Goal);
            Assert.Equal(expectedDraft, conversation.DraftText);
            Assert.Equal(expectedDraft, viewModel.InputText);
            Assert.Contains(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            Assert.False(runtime.Entered.IsCompleted);
            Assert.Empty(conversation.Messages);
            Assert.Empty(viewModel.QueuedFollowUps);
            Assert.Empty(state.QueuedFollowUpRecoveries);
        }
        finally
        {
            taskHost.Changed -= failSaveOnStart;
            Volatile.Write(ref failSaves, 0);
            releaseSave.TrySetResult();
            releaseActive.TrySetResult();
            viewModel.Dispose();
        }
    }

    [Theory]
    [InlineData("cancel-image", false)]
    [InlineData("cancel-image", true)]
    [InlineData("cancel-context", false)]
    [InlineData("cancel-context", true)]
    [InlineData("missing-image", false)]
    [InlineData("missing-image", true)]
    public async Task UnpreparedQueuedFollowUpFailureRestoresPromptAttachmentsAndNewerDraft(
        string failureKind,
        bool hasNewerDraft)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            nameof(UnpreparedQueuedFollowUpFailureRestoresPromptAttachmentsAndNewerDraft),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "source.png");
        await File.WriteAllBytesAsync(
            sourcePath,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        profile.SupportsImageInput = true;
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = failureKind == "cancel-context"
            ? CopilotAttachmentItem.CreateContext("queued context")
            : CopilotAttachmentItem.CreateImage(sourcePath, "queued image");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer attachment");
        conversation.Attachments.Add(queuedAttachment);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = [conversation],
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuedCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state, Path.Combine(root, "attachments")),
            CreateConfig(profile, "unprepared-queued-follow-up-test-token"),
            runtime,
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            _ => releaseActive.Task);
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? observeQueuedRun = null;

        try
        {
            viewModel.InputText = "inspect the queued attachment";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.False(Assert.Single(viewModel.QueuedFollowUps).IsLocalCommand);
            Assert.Equal(queuedRun.Id, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
            Assert.Empty(conversation.Attachments);
            Assert.Equal(string.Empty, viewModel.InputText);
            var shouldCancel = failureKind != "missing-image";
            var cancellationAccepted = false;
            observeQueuedRun = (_, args) =>
            {
                if (args.Run.Id != queuedRun.Id)
                    return;
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started && shouldCancel)
                    cancellationAccepted = taskHost.RequestCancel(queuedRun.Id);
                if (args.Kind == CopilotAgentTaskHostChangeKind.Completed)
                    queuedCompleted.TrySetResult();
            };
            taskHost.Changed += observeQueuedRun;
            if (hasNewerDraft)
            {
                viewModel.InputText = "newer draft";
                conversation.Attachments.Add(newerAttachment);
            }
            if (!shouldCancel)
                File.Delete(sourcePath);

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            if (shouldCancel)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    await queuedRun.Completion.WaitAsync(TestTimeout));
            }
            else
            {
                await queuedRun.Completion.WaitAsync(TestTimeout);
            }
            await queuedCompleted.Task.WaitAsync(TestTimeout);

            var expectedDraft = hasNewerDraft
                ? "newer draft" + Environment.NewLine + Environment.NewLine + "inspect the queued attachment"
                : "inspect the queued attachment";
            Assert.Equal(shouldCancel, cancellationAccepted);
            Assert.Equal(expectedDraft, conversation.DraftText);
            Assert.Equal(expectedDraft, viewModel.InputText);
            Assert.Single(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            if (hasNewerDraft)
                Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.False(runtime.Entered.IsCompleted);
            Assert.Empty(conversation.Messages);
            Assert.Empty(state.QueuedFollowUpRecoveries);
            Assert.Empty(viewModel.QueuedFollowUps);
        }
        finally
        {
            taskHost.Changed -= observeQueuedRun;
            releaseActive.TrySetResult();
            runtime.Release();
            viewModel.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QueuedImageIsCommittedToManagedStorageBeforeItsTurnStarts()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            nameof(QueuedImageIsCommittedToManagedStorageBeforeItsTurnStarts),
            Guid.NewGuid().ToString("N"));
        var attachmentDirectoryPath = Path.Combine(root, "attachments");
        var sourcePath = Path.Combine(root, "source.png");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(
            sourcePath,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        profile.SupportsImageInput = true;
        var config = CreateConfig(profile, "queued-image-admission-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var runtime = new GatedFailingTurnRuntime();
        var queuedFollowUp = new CopilotQueuedFollowUp(
            "queued-image-admission-run",
            conversation.Id,
            conversation.Title,
            "inspect the queued image",
            CopilotAgentMode.Auto,
            profile,
            new CopilotAgentHostContextSnapshot(
                "",
                "",
                [CopilotAttachmentItem.CreateImage(sourcePath, "Evidence")]));
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            runtime,
            new CopilotAgentTaskHost(),
            attachmentDirectoryPath);
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto);
        var executeMethod = typeof(CopilotChatViewModel).GetMethod(
            "ExecuteQueuedFollowUpAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExecuteQueuedFollowUpAsync was not found.");

        try
        {
            var execution = Assert.IsAssignableFrom<Task>(executeMethod.Invoke(
                viewModel,
                [hostedRun, queuedFollowUp]));
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            var admittedImage = Assert.Single(request.HostContext.Attachments);

            Assert.StartsWith(
                Path.Combine(attachmentDirectoryPath, "image-"),
                admittedImage.Value,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(admittedImage.Value));
            Assert.Equal(
                admittedImage.Value,
                Assert.Single(conversation.Messages, message => message.IsUser)
                    .Attachments.Single().Value);

            File.Delete(sourcePath);
            runtime.Release();
            await execution.WaitAsync(TestTimeout);
        }
        finally
        {
            runtime.Release();
            viewModel.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QueuedSlashCommandRunsInItsOriginConversationAndRestoresLaterSelection()
    {
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "queued-command-conversation-binding-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profileA, profileB },
        };
        var conversationA = CreateConversation(profileA, "conversation-a", string.Empty);
        var conversationB = CreateConversation(profileB, "conversation-b", "conversation B draft");
        var state = new CopilotChatState
        {
            ActiveConversationId = conversationA.Id,
            ActiveProfileId = profileA.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                conversationA,
                conversationB,
            },
        };
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversationA.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/status";
            viewModel.QueueFollowUpCommand.Execute(null);
            var queuedRun = Assert.Single(taskHost.QueuedRuns);

            Assert.True(viewModel.SelectConversationCommand.CanExecute(conversationB));
            viewModel.SelectConversationCommand.Execute(conversationB);
            Assert.Same(conversationB, viewModel.SelectedConversation);

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await queuedRun.Completion.WaitAsync(TestTimeout);

            Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);
            Assert.Equal("conversation B draft", viewModel.InputText);
            Assert.Empty(conversationA.Messages);
            Assert.Empty(conversationB.Messages);
        }
        finally
        {
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task QueuedPlanCommandStartsBeforeLaterFollowUpWithoutConsumingNewerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-plan-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var planAttachment = CopilotAttachmentItem.CreateContext("plan command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(planAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? planRun = null;
        CopilotQueuedFollowUp? laterFollowUp = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/plan inspect the deferred command path";
            viewModel.QueueFollowUpCommand.Execute(null);
            var commandItem = Assert.Single(viewModel.QueuedFollowUps);
            Assert.True(commandItem.IsLocalCommand);

            viewModel.InputText = "run after the plan";
            viewModel.QueueFollowUpCommand.Execute(null);
            laterFollowUp = Assert.Single(viewModel.QueuedFollowUps, item => !item.IsLocalCommand);
            viewModel.InputText = "newer draft";
            conversation.Attachments.Add(newerAttachment);

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            planRun = taskHost.ActiveRun;

            Assert.NotNull(planRun);
            Assert.Equal(CopilotAgentMode.Plan, request.Mode);
            Assert.Equal("inspect the deferred command path", request.UserText);
            Assert.Equal(1, taskHost.GetQueuePosition(laterFollowUp.RunId));
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Contains(request.HostContext.Attachments, attachment => attachment.Id == planAttachment.Id);
            Assert.DoesNotContain(conversation.Attachments, attachment => attachment.Id == planAttachment.Id);
            Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.DoesNotContain(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "/plan inspect the deferred command path", StringComparison.Ordinal));
            Assert.Contains(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "inspect the deferred command path", StringComparison.Ordinal));
        }
        finally
        {
            runtime.Release();
            if (planRun != null && !planRun.Completion.IsCompleted)
            {
                try
                {
                    await planRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            if (laterFollowUp != null)
                taskHost.RequestCancel(laterFollowUp.RunId);
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task QueuedRetryCommandSchedulesTheRetryWithoutSendingItsSlashText()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-retry-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "retry this request")
        {
            RequestMode = CopilotAgentMode.Auto,
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "old answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        });
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? retryRun = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/retry";
            viewModel.QueueFollowUpCommand.Execute(null);
            var commandRun = Assert.Single(taskHost.QueuedRuns);
            viewModel.InputText = "newer draft";

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            retryRun = taskHost.ActiveRun;

            Assert.NotNull(retryRun);
            Assert.True(retryRun.IsAgent);
            Assert.Equal("retry this request", request.UserText);
            Assert.Equal(CopilotAgentMode.Auto, request.Mode);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.DoesNotContain(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "/retry", StringComparison.Ordinal));
            await commandRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            runtime.Release();
            if (retryRun != null && !retryRun.Completion.IsCompleted)
            {
                try
                {
                    await retryRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task UnknownQueuedSlashCommandReportsLocallyWithoutCreatingAgentMessages()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-unknown-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        await activeStarted.Task.WaitAsync(TestTimeout);
        viewModel.InputText = "/command-that-does-not-exist";
        viewModel.QueueFollowUpCommand.Execute(null);
        var queuedRun = Assert.Single(taskHost.QueuedRuns);

        releaseActive.TrySetResult();
        await activeRun.Completion.WaitAsync(TestTimeout);
        await queuedRun.Completion.WaitAsync(TestTimeout);

        Assert.NotEqual(string.Empty, viewModel.LocalCommandResultTitle);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public async Task QueuedStopCommandDoesNotCancelItsDispatcherOrBlockTheNextFollowUp()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-stop-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? followUpRun = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/stop";
            viewModel.QueueFollowUpCommand.Execute(null);
            viewModel.InputText = "run after queued stop";
            viewModel.QueueFollowUpCommand.Execute(null);

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            followUpRun = taskHost.ActiveRun;

            Assert.Equal("run after queued stop", request.UserText);
            Assert.Contains("没有正在运行的任务", viewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        finally
        {
            runtime.Release();
            if (followUpRun != null && !followUpRun.Completion.IsCompleted)
            {
                try
                {
                    await followUpRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task RestartedQueuedSlashCommandKeepsItsKindAndExecutesLocally()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "restarted-queued-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries =
            [
                new CopilotQueuedFollowUpRecoveryRecord
                {
                    RunId = "queued-command-restart",
                    ConversationId = conversation.Id,
                    Prompt = "/status",
                    ComposerState = CopilotComposerStash.Capture(
                        "/status",
                        "/status".Length,
                        CopilotAgentMode.Auto,
                        []),
                    ProfileId = profile.Id,
                    QueuedAtUtc = new DateTimeOffset(2026, 8, 11, 16, 0, 0, TimeSpan.Zero),
                    ResumeAfterRestart = true,
                    IsLocalCommand = true,
                },
            ],
        };
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var restoredRun = taskHost.ActiveRun ?? taskHost.QueuedRuns.SingleOrDefault();

        if (restoredRun != null)
        {
            Assert.False(restoredRun.IsAgent);
            Assert.Null(restoredRun.RunControl);
            await restoredRun.Completion.WaitAsync(TestTimeout);
        }

        Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
        Assert.Empty(conversation.Messages);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    private static CopilotConfig CreateConfig(CopilotProfileConfig profile, string bearerToken) => new()
    {
        SchemaVersion = CopilotConfig.CurrentSchemaVersion,
        McpBearerToken = bearerToken,
        Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
    };

    private static CopilotConversationRecord CreateConversation(
        CopilotProfileConfig profile,
        string id,
        string draftText)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = id;
        conversation.DraftText = draftText;
        return conversation;
    }

    private static CopilotChatViewModel CreateViewModel(
        CopilotConversationRecord conversation,
        CopilotConfig config,
        ICopilotTurnRuntime runtime,
        CopilotAgentTaskHost taskHost,
        string attachmentDirectoryPath = "")
    {
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = conversation.ProfileId,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        return new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state, attachmentDirectoryPath),
            config,
            runtime,
            taskHost);
    }

    private static void ApplyAgentEvents(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotChatMessage assistantMessage,
        params CopilotAgentEvent[] agentEvents)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "ApplyAgentEvents",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ApplyAgentEvents was not found.");
        method.Invoke(
            viewModel,
            [hostedRun, conversation, assistantMessage, agentEvents]);
    }

    private static bool CommitAgentRunStateAndResolveSteering(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotAgentTaskEventJournalSnapshot journal,
        CopilotAgentSessionCheckpoint checkpoint,
        CopilotAgentStopReason stopReason)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "CommitAgentRunStateAndResolveSteering",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CommitAgentRunStateAndResolveSteering was not found.");
        return Assert.IsType<bool>(method.Invoke(
            viewModel,
            [hostedRun, conversation, journal, checkpoint, stopReason]));
    }

    private static async Task ProcessGoalAfterTurnAsync(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotProfileConfig profile,
        CopilotChatMessage userMessage,
        CopilotChatMessage assistantMessage,
        string boundGoalId,
        CopilotTokenUsage turnUsage)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "ProcessGoalAfterTurnAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProcessGoalAfterTurnAsync was not found.");
        var task = (Task?)method.Invoke(
            viewModel,
            [
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                boundGoalId,
                turnUsage,
            ]);
        Assert.NotNull(task);
        await task;
    }

    private static bool TryQueueGoalContinuation(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotChatMessage assistantMessage,
        CopilotProfileConfig profile,
        CopilotAgentHostContextSnapshot completedTurnSnapshot,
        CopilotTurnRuntimeConfigSnapshot completedTurnRuntimeConfig,
        string goalId,
        string reason)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "TryQueueGoalContinuation",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryQueueGoalContinuation was not found.");
        return Assert.IsType<bool>(method.Invoke(
            viewModel,
            [
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                completedTurnRuntimeConfig,
                goalId,
                reason,
            ]));
    }

    private static async Task CompleteAndDisposeAsync(
        CopilotChatViewModel viewModel,
        GatedFailingTurnRuntime runtime,
        CopilotAgentTaskHost taskHost,
        CopilotHostedAgentRun? activeRun)
    {
        runtime.Release();
        var pendingRun = activeRun ?? taskHost.ActiveRun;
        if (pendingRun != null && !pendingRun.Completion.IsCompleted)
        {
            try
            {
                await pendingRun.Completion.WaitAsync(TestTimeout);
            }
            catch
            {
            }
        }
        viewModel.Dispose();
    }

    private static CopilotProfileConfig CreateProfile(string id, string name, string model) => new()
    {
        Id = id,
        Name = name,
        VendorType = CopilotVendorType.Custom,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "profile-isolation-test-key",
        BaseUrl = "https://unit.test/v1",
        Model = model,
    };

    private sealed class GatedFailingTurnRuntime : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CopilotTurnRequest> Entered => _entered.Task;

        private readonly TaskCompletionSource<CopilotTurnRequest> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _entered.TrySetResult(request);
            await _release.Task.WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent("profile-isolation-turn", request.Mode);
            throw new InvalidOperationException("Expected profile-isolation test failure.");
        }

        public void Release() => _release.TrySetResult();

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) =>
            new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class AcceptingQuestionTurnRuntime : ICopilotTurnRuntime
    {
        public string Answer { get; private set; } = string.Empty;

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) =>
            new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer)
        {
            Answer = answer;
            return true;
        }

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class InMemoryStateStore(
        CopilotChatState state,
        string attachmentDirectoryPath = "",
        Func<CancellationToken, Task>? saveSerializedAsync = null) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath { get; } = attachmentDirectoryPath;

        public CopilotChatState Load() => state;

        public void Save(CopilotChatState value)
        {
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) =>
            new(new JObject());

        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";

        public string Serialize(CopilotChatState value) => "{}";

        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return saveSerializedAsync?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class GatedGoalCompletionEvaluator(CopilotTokenUsage usage) : ICopilotGoalCompletionEvaluator
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async Task<CopilotGoalEvaluationResult> EvaluateAsync(
            CopilotProfileConfig profile,
            CopilotConversationGoal goal,
            IReadOnlyList<CopilotRequestMessage> transcript,
            CopilotGoalTurnEvidence turnEvidence,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new CopilotGoalEvaluationResult(
                CopilotGoalEvaluationVerdict.Continue,
                "More verified work remains.",
                usage);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class CancellingGoalCompletionEvaluator(
        Action requestCancellation,
        CopilotTokenUsage usage) : ICopilotGoalCompletionEvaluator
    {
        public Task<CopilotGoalEvaluationResult> EvaluateAsync(
            CopilotProfileConfig profile,
            CopilotConversationGoal goal,
            IReadOnlyList<CopilotRequestMessage> transcript,
            CopilotGoalTurnEvidence turnEvidence,
            CancellationToken cancellationToken)
        {
            requestCancellation();
            return Task.FromResult(new CopilotGoalEvaluationResult(
                CopilotGoalEvaluationVerdict.Continue,
                "More verified work remains.",
                usage));
        }
    }

    private sealed class IsolatedSolutionManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField(
            "_instance",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SolutionManager singleton field was not found.");

        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance =
            (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope()
        {
            InstanceField.SetValue(null, _testInstance);
        }

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }

    private sealed class PausedAttachmentSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public ManualResetEventSlim CallbackPosted { get; } = new();

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            CallbackPosted.Set();
        }

        public void RunPending()
        {
            while (_callbacks.TryDequeue(out var callback))
                callback.Callback(callback.State);
        }

        public void Dispose() => CallbackPosted.Dispose();
    }

    private static void RunAttachmentTestOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "The STA clipboard lifecycle test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
