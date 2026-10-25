using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotGitDiffFeedbackTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("newer", "success")]
    [InlineData("newer", "throw")]
    [InlineData("switch", "failure")]
    [InlineData("dispose", "success")]
    [InlineData("unchanged", "success")]
    [InlineData("unchanged", "throw")]
    public async Task GitDiffCompletionPreservesTheCurrentFeedbackOwner(string transition, string outcome)
    {
        await using var fixture = new Fixture();
        var operation = fixture.Start(outcome);
        await fixture.Entered.Task.WaitAsync(TestTimeout);
        Assert.False(operation.IsCompleted);
        Assert.False(fixture.ViewModel.IsBusy);
        Assert.True(fixture.ViewModel.CanSwitchConversation);
        if (transition == "newer")
        {
            fixture.ViewModel.InputText = "/help";
            Assert.True(fixture.ViewModel.SendCommand.CanExecute(null));
            fixture.ViewModel.SendCommand.Execute(null);
            Assert.StartsWith("/help", fixture.ViewModel.LocalCommandResultTitle, StringComparison.Ordinal);
        }
        else if (transition == "switch")
            Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Other.Id));
        else if (transition == "dispose")
        {
            fixture.ViewModel.Dispose();
            Assert.True(fixture.CapturedToken.IsCancellationRequested);
        }
        var expectedTitle = fixture.ViewModel.LocalCommandResultTitle;
        var expectedText = fixture.ViewModel.LocalCommandResultText;
        var expectedOwner = fixture.ViewModel.SelectedConversation;

        fixture.Release.TrySetResult();
        await operation.WaitAsync(TestTimeout);

        Assert.Equal(1, fixture.StatusCalls);
        Assert.Equal(outcome == "success" ? 1 : 0, fixture.DiffCalls);
        Assert.Same(expectedOwner, fixture.ViewModel.SelectedConversation);
        Assert.Equal(0, fixture.Runtime.Calls);
        Assert.Empty(fixture.Host.ScheduledRuns);
        if (transition == "unchanged")
        {
            Assert.StartsWith("/diff", fixture.ViewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Contains(outcome == "success" ? "fixture patch" : "Git 变更快照失败", fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expectedTitle, fixture.ViewModel.LocalCommandResultTitle);
            Assert.Equal(expectedText, fixture.ViewModel.LocalCommandResultText);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private Task? _operation;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken CapturedToken { get; private set; }
        public int StatusCalls { get; private set; }
        public int DiffCalls { get; private set; }

        public Fixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var profile = new CopilotProfileConfig
            {
                Id = "diff-feedback-profile", Name = "Diff feedback fixture",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "unused-test-key", BaseUrl = "https://example.test/v1", Model = "fixture-model",
            };
            var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Other = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation, Other],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state),
                new CopilotConfig { Profiles = [profile], McpBearerToken = "unused-test-token" }, Runtime, Host);
        }

        public CopilotChatViewModel ViewModel { get; }
        public CopilotConversationRecord Other { get; }
        public IdleRuntime Runtime { get; } = new();
        public CopilotAgentTaskHost Host { get; } = new();

        public Task Start(string outcome)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
            var service = new CopilotLocalGitDiffService(async (_, _, token) =>
            {
                StatusCalls++;
                CapturedToken = token;
                Entered.TrySetResult();
                // A late result may ignore cancellation; the feedback owner still has to be checked.
                await Release.Task;
                if (outcome == "throw") throw new IOException("The fixture Git read failed.");
                return new CopilotToolResult
                {
                    Success = outcome == "success", Summary = "The fixture Git read returned a result.",
                    Content = "result_json: " + JsonSerializer.Serialize(new { repository_root = root, branch = "fixture-branch" }),
                };
            }, (_, _, _) =>
            {
                DiffCalls++;
                return Task.FromResult(new CopilotToolResult
                {
                    Success = true,
                    Content = CopilotGitDiffResultProtocol.Serialize(new CopilotGitDiffSnapshot(root, "both", string.Empty,
                        true, true, false,
                        [new("unstaged", true, true, false, "fixture patch"),
                         new("staged", false, true, false, string.Empty),
                         new("untracked", false, true, false, string.Empty)])
                    {
                        ChangedPaths = ["fixture.txt"], ChangedPathsComplete = true,
                    }),
                });
            });
            typeof(CopilotChatViewModel).GetField("_localGitDiffService", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(ViewModel, service);
            var command = CopilotLocalCommandCatalog.FindExact("/diff")!;
            _operation = (Task)typeof(CopilotChatViewModel).GetMethod("ShowGitDiffAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(ViewModel, [command, "both"])!;
            return _operation;
        }

        public async ValueTask DisposeAsync()
        {
            ViewModel.Dispose();
            Release.TrySetResult();
            try
            {
                if (_operation != null) await _operation.WaitAsync(TestTimeout);
            }
            finally
            {
                if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                    SolutionInstance.SetValue(null, _previousSolution);
            }
        }
    }

    private sealed class IdleRuntime : ICopilotTurnRuntime
    {
        public int Calls { get; private set; }
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new NotSupportedException();
        }
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot value) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }
}
