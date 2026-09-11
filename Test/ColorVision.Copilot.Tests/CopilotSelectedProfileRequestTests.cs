using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotSelectedProfileRequestTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(CopilotAgentMode.Auto, false)]
    [InlineData(CopilotAgentMode.Code, false)]
    [InlineData(CopilotAgentMode.Plan, false)]
    [InlineData(CopilotAgentMode.Review, false)]
    [InlineData(CopilotAgentMode.Chat, false)]
    [InlineData(CopilotAgentMode.Review, true)]
    public async Task SendKeepsSelectedProfileAndComposesInstructionsOnAnIndependentSnapshot(
        CopilotAgentMode mode,
        bool hasSystemPromptOverride)
    {
        var profile = new CopilotProfileConfig
        {
            Id = "selected-profile",
            Name = "Selected profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            BaseUrl = "https://example.test/v1",
            ApiKey = "selected-profile-test-key",
            Model = "selected-profile-model",
            MaxTokens = 4_096,
            Temperature = 0.7,
        };
        if (hasSystemPromptOverride)
            profile.UseSystemPromptOverride("Selected profile system instructions.");
        var originalSystemPrompt = profile.EffectiveSystemPrompt;
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.DraftRequestMode = mode;
        conversation.ResponsePersonality = CopilotResponsePersonality.Friendly;
        conversation.HasResponsePersonalityOverride = true;
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = [conversation],
        };
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "selected-profile-test-token",
            Profiles = [profile],
        };
        var runtime = new CapturingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(), new InMemoryStateStore(state), config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        try
        {
            Assert.Same(profile, viewModel.SelectedProfile);
            viewModel.InputText = "Explain the selected design.";
            viewModel.SendCommand.Execute(null);

            var request = await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(mode, request.Mode);
            Assert.Equal(conversation.Id, request.ConversationId);
            Assert.NotSame(profile, request.Profile);
            Assert.Equal(profile.Id, request.Profile.Id);
            Assert.Equal(profile.Name, request.Profile.Name);
            Assert.Equal(profile.VendorType, request.Profile.VendorType);
            Assert.Equal(profile.ProviderType, request.Profile.ProviderType);
            Assert.Equal(profile.BaseUrl, request.Profile.BaseUrl);
            Assert.Equal(profile.ApiKey, request.Profile.ApiKey);
            Assert.Equal(profile.Model, request.Profile.Model);
            Assert.Equal(profile.MaxTokens, request.Profile.MaxTokens);
            Assert.Equal(profile.Temperature, request.Profile.Temperature);
            var requestSystemPrompt = request.Profile.EffectiveSystemPrompt;
            Assert.StartsWith(originalSystemPrompt + "\n\n", requestSystemPrompt, StringComparison.Ordinal);
            Assert.Contains("format it as a Markdown link", requestSystemPrompt, StringComparison.Ordinal);
            Assert.Contains("Use a warm, collaborative communication style", requestSystemPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Use a pragmatic, outcome-first communication style", requestSystemPrompt, StringComparison.Ordinal);
            Assert.Contains(
                CopilotResponsePresentationGuidance.BuildResponseLanguageInstruction(CultureInfo.CurrentUICulture),
                requestSystemPrompt,
                StringComparison.Ordinal);
            Assert.Equal(originalSystemPrompt, profile.EffectiveSystemPrompt);
            Assert.Equal(hasSystemPromptOverride, profile.HasSystemPromptOverride);

            profile.Model = "next-request-model";
            profile.ApiKey = "next-request-test-key";
            profile.UseSystemPromptOverride("Next request system instructions.");
            conversation.ResponsePersonality = CopilotResponsePersonality.Pragmatic;
            Assert.Equal("selected-profile-model", request.Profile.Model);
            Assert.Equal("selected-profile-test-key", request.Profile.ApiKey);
            Assert.Equal(requestSystemPrompt, request.Profile.EffectiveSystemPrompt);
        }
        finally
        {
            runtime.Release();
            var pendingRun = activeRun ?? taskHost.ActiveRun;
            if (pendingRun != null)
                await pendingRun.Completion.WaitAsync(TestTimeout);
        }
    }

    private sealed class CapturingTurnRuntime : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource<CopilotTurnRequest> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CopilotTurnRequest> Entered => _entered.Task;

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _entered.TrySetResult(request);
            await _release.Task.WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return CopilotTurnCompletedEvent.Interrupted(request.TaskId, request.Mode);
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

    private sealed class InMemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;

        public CopilotChatState Load() => state;

        public void Save(CopilotChatState value) { }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());

        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";

        public string Serialize(CopilotChatState value) => "{}";

        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class IsolatedSolutionManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField(
            "_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous = InstanceField.GetValue(null);
        private readonly SolutionManager _replacement =
            (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope() => InstanceField.SetValue(null, _replacement);

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _replacement))
                InstanceField.SetValue(null, _previous);
        }
    }
}
