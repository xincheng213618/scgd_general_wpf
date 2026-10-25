using ColorVision.Solution;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotQueuedProfileRecoveryTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string FirstPrompt = "Inspect the original request with its original profile.";
    private const string SuccessorPrompt = "Continue only after the preceding request is accepted.";
    private const string IndependentPrompt = "Continue the independent conversation.";
    private const string NewerDraft = "Keep this newer draft.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnusableOriginalProfileRestoresItsQueueAndSuccessorsToDraftWithoutChangingModels(bool incomplete)
    {
        await using var fixture = new Fixture(incomplete);
        var request = await fixture.Runtime.Entered.Task.WaitAsync(TestTimeout);

        Assert.Equal(IndependentPrompt, request.UserText);
        Assert.Equal(fixture.AvailableProfileId, request.Profile.Id);
        var source = Assert.Single(fixture.ViewModel.Conversations, conversation => conversation.Id == fixture.SourceConversationId);
        Assert.Equal(NewerDraft + Environment.NewLine + Environment.NewLine
            + CopilotQueuedFollowUpRecovery.FormatRestoredDraft([FirstPrompt, SuccessorPrompt]), source.DraftText);
        Assert.Equal(new[] { "newer context", "first captured context", "successor captured context" },
            source.Attachments.Select(attachment => attachment.Value));
        Assert.Equal(fixture.AttachmentIds, source.Attachments.Select(attachment => attachment.Id));
        Assert.Empty(source.Messages);
        Assert.Equal(source.DraftText, fixture.ViewModel.InputText);
        Assert.Empty(fixture.ViewModel.QueuedFollowUps);
        Assert.Single(fixture.Runtime.Requests);
        Assert.DoesNotContain(fixture.Runtime.Requests, captured => captured.ConversationId == fixture.SourceConversationId);
        await fixture.FlushAsync();
        var reloaded = new CopilotChatStateStore(fixture.Root).Load();
        Assert.Empty(reloaded.QueuedFollowUpRecoveries);
        var savedSource = Assert.Single(reloaded.Conversations, conversation => conversation.Id == fixture.SourceConversationId);
        Assert.Equal(source.DraftText, savedSource.DraftText);
        Assert.Equal(fixture.AttachmentIds, savedSource.Attachments.Select(attachment => attachment.Id));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotProfileQueueRecovery-").FullName;
        public string SourceConversationId { get; }
        public string AvailableProfileId { get; }
        public string[] AttachmentIds { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public CapturingRuntime Runtime { get; } = new();

        public Fixture(bool incomplete)
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var originalProfile = Profile("original-profile");
            if (incomplete)
                originalProfile.ApiKey = string.Empty;
            else
                originalProfile.IsEnabled = false;
            var availableProfile = Profile("available-profile");
            AvailableProfileId = availableProfile.Id;
            Assert.False(originalProfile.IsConfigured);
            Assert.True(availableProfile.IsConfigured);
            var source = CopilotConversationRecord.CreateEmpty(originalProfile.Id, originalProfile.DisplayLabel);
            SourceConversationId = source.Id;
            source.DraftText = NewerDraft;
            var newerAttachment = CopilotAttachmentItem.CreateContext("newer context");
            var firstAttachment = CopilotAttachmentItem.CreateContext("first captured context");
            var successorAttachment = CopilotAttachmentItem.CreateContext("successor captured context");
            source.Attachments.Add(newerAttachment);
            AttachmentIds = [newerAttachment.Id, firstAttachment.Id, successorAttachment.Id];
            var independent = CopilotConversationRecord.CreateEmpty(availableProfile.Id, availableProfile.DisplayLabel);
            var state = new CopilotChatState
            {
                ActiveConversationId = source.Id, ActiveProfileId = availableProfile.Id,
                Conversations = [source, independent],
                QueuedFollowUpRecoveries =
                [
                    Recovery("unusable-first", source.Id, originalProfile.Id, FirstPrompt, [firstAttachment]),
                    Recovery("valid-successor", source.Id, availableProfile.Id, SuccessorPrompt, [successorAttachment]),
                    Recovery("independent", independent.Id, availableProfile.Id, IndependentPrompt, []),
                ],
            };
            new CopilotChatStateStore(Root).Save(state);
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new CopilotChatStateStore(Root),
                new CopilotConfig
                {
                    SchemaVersion = CopilotConfig.CurrentSchemaVersion, Profiles = [originalProfile, availableProfile],
                    McpBearerToken = "unused-profile-recovery-token",
                }, Runtime, Host);
        }

        public Task FlushAsync() => ((Task)typeof(CopilotChatViewModel)
            .GetMethod("FlushStatePersistenceBarrierAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ViewModel, null)!).WaitAsync(TestTimeout);

        public async ValueTask DisposeAsync()
        {
            var scheduled = Host.ScheduledRuns.ToArray();
            Host.Shutdown();
            Runtime.Release.TrySetResult();
            try
            {
                foreach (var run in scheduled)
                {
                    var exception = await Record.ExceptionAsync(() => run.Completion.WaitAsync(TestTimeout));
                    Assert.IsNotType<TimeoutException>(exception);
                }
                await FlushAsync();
            }
            finally
            {
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                    SolutionInstance.SetValue(null, _previousSolution);
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root));
                var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(root).StartsWith("CopilotProfileQueueRecovery-", StringComparison.Ordinal));
                Directory.Delete(root, recursive: true);
            }
        }

        private static CopilotProfileConfig Profile(string id) => new()
        {
            Id = id, Name = id, VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible, ApiKey = "unused-recovery-key",
            BaseUrl = "https://example.test/v1", Model = id + "-model",
        };

        private static CopilotQueuedFollowUpRecoveryRecord Recovery(string id, string conversationId, string profileId,
            string prompt, CopilotAttachmentItem[] attachments) => new()
        {
            RunId = id, ConversationId = conversationId, ProfileId = profileId, Prompt = prompt,
            ResumeAfterRestart = true,
            ComposerState = CopilotComposerStash.Capture(prompt, prompt.Length, CopilotAgentMode.Auto, attachments),
        };
    }

    private sealed class CapturingRuntime : ICopilotTurnRuntime
    {
        public TaskCompletionSource<CopilotTurnRequest> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            Entered.TrySetResult(request);
            await Release.Task.WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return CopilotTurnCompletedEvent.Interrupted(request.TaskId, request.Mode);
        }
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
