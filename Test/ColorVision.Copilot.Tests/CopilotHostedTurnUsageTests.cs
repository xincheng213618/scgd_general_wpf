using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotHostedTurnUsageTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly CopilotTokenUsage PreviousUsage = new(900, 90, 990, 400);
    private static readonly CopilotTokenUsage CurrentUsage = new(120, 30, 150, 80);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task HostedChatInterruptionSettlesTheUsageReceivedThroughTurnEvents(bool cancelled, bool reportUsage)
    {
        await using var fixture = new UsageFixture(reportUsage);
        fixture.ViewModel.QueueExternalPrompt("Reply to this test request", startNewConversation: false, sendNow: true, mode: CopilotAgentMode.Chat);
        await fixture.Runtime.UsageProcessed.WaitAsync(TestTimeout);
        var assistant = fixture.Conversation.Messages.Last(message => !message.IsUser);
        var expectedUsage = reportUsage ? CurrentUsage : CopilotTokenUsage.Empty;
        Assert.Equal(expectedUsage, assistant.ReportedUsage);
        Assert.Equal(PreviousUsage, fixture.Conversation.LastUsage);
        var run = Assert.IsType<CopilotHostedAgentRun>(fixture.Host.ActiveRun);

        if (cancelled)
            Assert.True(fixture.Host.RequestCancel(run.Id));
        else
            fixture.Runtime.Release();
        try
        {
            await run.Completion.WaitAsync(TestTimeout);
        }
        catch (OperationCanceledException) when (cancelled)
        {
        }

        Assert.True(assistant.WasResponseInterrupted);
        Assert.Contains("Partial provider answer", assistant.Content, StringComparison.Ordinal);
        Assert.Equal(expectedUsage, assistant.ReportedUsage);
        Assert.Equal(expectedUsage, fixture.Conversation.LastUsage);
        Assert.Equal(PreviousUsage, fixture.PreviousAssistant.ReportedUsage);
    }

    private sealed class UsageFixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstanceField = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousSolutionInstance = SolutionInstanceField.GetValue(null);
        private readonly object _testSolutionInstance = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Path.Combine(Path.GetTempPath(), "CopilotHostedUsage-" + Guid.NewGuid().ToString("N"));

        public CopilotConversationRecord Conversation { get; }
        public CopilotChatMessage PreviousAssistant { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public UsageThenFailureRuntime Runtime { get; }

        public UsageFixture(bool reportUsage)
        {
            SolutionInstanceField.SetValue(null, _testSolutionInstance);
            Directory.CreateDirectory(_root);
            var profile = new CopilotProfileConfig
            {
                Id = "hosted-usage-profile",
                Name = "Hosted usage profile",
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "hosted-usage-test-key",
                BaseUrl = "https://unit.test/v1",
                Model = "test-model",
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Earlier request"));
            PreviousAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Earlier answer");
            PreviousAssistant.SetReportedUsage(PreviousUsage);
            Conversation.Messages.Add(PreviousAssistant);
            Conversation.SetLastUsage(PreviousUsage);
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id,
                ActiveProfileId = profile.Id,
                Conversations = [Conversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "hosted-usage-test-token",
                Profiles = [profile],
            };
            Runtime = new UsageThenFailureRuntime(reportUsage);
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state, _root), config, Runtime, Host);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Runtime.Release();
                if (Host.ActiveRun is { } active)
                {
                    try
                    {
                        await active.Completion.WaitAsync(TestTimeout);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            finally
            {
                Host.Shutdown();
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstanceField.GetValue(null), _testSolutionInstance))
                    SolutionInstanceField.SetValue(null, _previousSolutionInstance);
                var fullRoot = Path.GetFullPath(_root);
                var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(fullRoot).StartsWith("CopilotHostedUsage-", StringComparison.Ordinal))
                {
                    Directory.Delete(fullRoot, recursive: true);
                }
            }
        }
    }

    private sealed class MemoryStateStore(CopilotChatState state, string root) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => root;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class UsageThenFailureRuntime(bool reportUsage) : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _usageProcessed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task UsageProcessed => _usageProcessed.Task;
        public void Release() => _release.TrySetResult();

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return new CopilotTurnRequestPreparedEvent(new CopilotPreparedTurnRequest(request.UserText, false));
            yield return new CopilotTurnChatDeltaEvent(new CopilotStreamDelta(string.Empty, "Partial provider answer"));
            var events = new List<CopilotTurnEvent>();
            var sink = new CopilotTurnEventSink(events.Add);
            sink.OnTokenUsageUpdated(reportUsage ? CurrentUsage : CopilotTokenUsage.Empty);
            foreach (var turnEvent in events)
                yield return turnEvent;
            _usageProcessed.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            throw new InvalidOperationException("Expected provider failure after partial answer.");
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
