using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotBackgroundStopFeedbackTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("dispose", "exception")]
    [InlineData("unchanged", "stopped")]
    public void BackgroundStopKeepsItsAuthorizedOperationAndOnlyPublishesCurrentFeedback(string transition, string outcome)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(TestTimeout, "The background stop did not drain.");
            var completion = new TaskCompletionSource<CopilotBackgroundShellCommandStopResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var command = Assert.IsType<CopilotLocalCommand>(CopilotLocalCommandCatalog.FindExact("/ps"));
            var stopCalls = 0;
            fixture.ViewModel.InputText = "/help";
            fixture.ViewModel.SendCommand.Execute(null);
            var previousContext = SynchronizationContext.Current;
            Task? stop = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                stop = ObserveStop(fixture.ViewModel, () => fixture.ViewModel.StopBackgroundShellCommandCoreAsync(
                    command, fixture.Conversation.Id, "fixture-background", 1, (conversationId, backgroundId, token) =>
                    {
                        Assert.Equal(fixture.Conversation.Id, conversationId);
                        Assert.Equal("fixture-background", backgroundId);
                        Assert.False(token.CanBeCanceled);
                        stopCalls++;
                        return completion.Task;
                    }));
                Assert.False(stop.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.True(fixture.ViewModel.CanSwitchConversation);
                if (transition == "help")
                {
                    // An identical report is still a newer user action and owns the feedback.
                    fixture.ViewModel.InputText = "/help";
                    Assert.True(fixture.ViewModel.SendCommand.CanExecute(null));
                    fixture.ViewModel.SendCommand.Execute(null);
                }
                else if (transition == "switch")
                {
                    Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Other.Id));
                }
                else if (transition == "dispose")
                {
                    fixture.ViewModel.Dispose();
                }
                var expectedTitle = fixture.ViewModel.LocalCommandResultTitle;
                var expectedText = fixture.ViewModel.LocalCommandResultText;
                var selected = fixture.ViewModel.SelectedConversation;

                if (outcome == "exception")
                    completion.SetException(new InvalidOperationException("The fixture stop failed unexpectedly."));
                else if (outcome == "failure")
                    completion.SetResult(new(null, CopilotToolFailureKind.Internal, "The fixture stop was rejected."));
                else
                    completion.SetResult(new(CreateSnapshot(fixture.Conversation.Id, CopilotBackgroundShellCommandState.Stopped),
                        CopilotToolFailureKind.None, string.Empty));
                context.Complete(stop);
                stop.GetAwaiter().GetResult();

                Assert.Equal(1, stopCalls);
                Assert.Same(selected, fixture.ViewModel.SelectedConversation);
                if (transition != "unchanged")
                {
                    Assert.Equal(expectedTitle, fixture.ViewModel.LocalCommandResultTitle);
                    Assert.Equal(expectedText, fixture.ViewModel.LocalCommandResultText);
                }
                else if (outcome == "exception")
                {
                    Assert.Equal("停止后台命令 · 失败", fixture.ViewModel.LocalCommandResultTitle);
                    Assert.Contains("The fixture stop failed unexpectedly.", fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
                }
                else
                {
                    Assert.StartsWith("/ps", fixture.ViewModel.LocalCommandResultTitle, StringComparison.Ordinal);
                    Assert.Contains(outcome == "failure" ? "后台命令未停止" : "已停止后台命令 #1 的进程树",
                        fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
                }
            }
            finally
            {
                try
                {
                    completion.TrySetResult(new(null, CopilotToolFailureKind.Internal, "Fixture cleanup."));
                    SynchronizationContext.SetSynchronizationContext(context);
                    if (stop is { IsCompleted: false })
                        context.Complete(stop);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
        }, TimeSpan.FromSeconds(40), "The background stop feedback test did not finish.");
    }

    [Theory]
    [InlineData((int)CopilotBackgroundShellCommandState.Completed, "已完成")]
    [InlineData((int)CopilotBackgroundShellCommandState.Failed, "失败")]
    [InlineData((int)CopilotBackgroundShellCommandState.Expired, "已到期")]
    public void AlreadyEndedBackgroundCommandDoesNotClaimItsProcessTreeWasStopped(int terminalState, string stateLabel)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            var command = Assert.IsType<CopilotLocalCommand>(CopilotLocalCommandCatalog.FindExact("/ps"));
            var state = (CopilotBackgroundShellCommandState)terminalState;
            var calls = 0;
            ObserveStop(fixture.ViewModel, () => fixture.ViewModel.StopBackgroundShellCommandCoreAsync(
                command, fixture.Conversation.Id, "fixture-background", 1, (_, _, _) =>
                {
                    calls++;
                    return Task.FromResult(new CopilotBackgroundShellCommandStopResult(
                        CreateSnapshot(fixture.Conversation.Id, state), CopilotToolFailureKind.None, string.Empty));
                })).GetAwaiter().GetResult();

            Assert.Equal(1, calls);
            Assert.StartsWith("/ps", fixture.ViewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Contains(stateLabel, fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
            Assert.DoesNotContain("已停止后台命令 #1 的进程树", fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
        });
    }

    private static Task ObserveStop(CopilotChatViewModel viewModel, Func<Task> operation) =>
        CopilotUiTaskObserver.ObserveAsync(operation, "停止后台命令", message =>
        {
            typeof(CopilotChatViewModel).GetProperty(nameof(CopilotChatViewModel.LocalCommandResultTitle))!
                .SetValue(viewModel, "停止后台命令 · 失败");
            typeof(CopilotChatViewModel).GetProperty(nameof(CopilotChatViewModel.LocalCommandResultText))!
                .SetValue(viewModel, message);
        });

    private static CopilotBackgroundShellCommandSnapshot CreateSnapshot(string conversationId, CopilotBackgroundShellCommandState state) =>
        new("fixture-background", conversationId, "fixture-task", CopilotShellKind.PowerShell,
            @"C:\fixture-workspace", "A controlled process fixture; never launched.", "fixture-sha256",
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, 123, true, state,
            state == CopilotBackgroundShellCommandState.Failed ? 1 : 0, string.Empty, string.Empty);

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public Fixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var profile = new CopilotProfileConfig
            {
                Id = "background-stop-profile", Name = "Background stop fixture",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "unused-test-key", BaseUrl = "https://example.test/v1", Model = "fixture-model",
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Other = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id,
                Conversations = [Conversation, Other],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state),
                new CopilotConfig { Profiles = [profile], McpBearerToken = "unused-test-token" }, new IdleRuntime(), Host);
        }

        public CopilotChatViewModel ViewModel { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord Other { get; }
        public CopilotAgentTaskHost Host { get; } = new();

        public void Dispose()
        {
            ViewModel.Dispose();
            Host.Shutdown();
            if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                SolutionInstance.SetValue(null, _previousSolution);
        }
    }

    private sealed class IdleRuntime : ICopilotTurnRuntime
    {
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
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
