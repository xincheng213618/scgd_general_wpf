using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotModelConnectionDiagnosticLifecycleTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionModelDiagnostic-" + Guid.NewGuid().ToString("N"));

    public CopilotModelConnectionDiagnosticLifecycleTests() => Directory.CreateDirectory(_rootDirectory);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CurrentProfileReceivesCompletedDiagnostic(bool succeeds)
    {
        using var handler = new DeferredModelHandler(succeeds);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var context = new PausedSynchronizationContext();
        var diagnostic = StartDiagnostic(viewModel, context);

        handler.Release.TrySetResult();
        await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(diagnostic.IsCompleted);
        context.RunPending();
        await diagnostic.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsTestingSelectedProfileConnection);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.False(viewModel.HasUnsavedSettings);
        Assert.StartsWith(succeeds ? "Connected in " : "Connection failed in ", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
        if (succeeds)
        {
            Assert.Contains("2 displayable characters", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
            Assert.Contains("Model test succeeded for Profile A.", viewModel.SettingsStatusText, StringComparison.Ordinal);
        }
        Assert.Equal(1, handler.RequestCount);
        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("test-model-a", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal(128, payload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0, payload.RootElement.GetProperty("temperature").GetDouble());
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Theory]
    [InlineData("switch", true)]
    [InlineData("switch", false)]
    [InlineData("switch-back", true)]
    [InlineData("switch-back", false)]
    [InlineData("edit", true)]
    [InlineData("edit", false)]
    [InlineData("edit-back", true)]
    [InlineData("edit-back", false)]
    [InlineData("clear", true)]
    [InlineData("clear", false)]
    [InlineData("clear-with-notice", true)]
    [InlineData("clear-with-notice", false)]
    [InlineData("close", true)]
    [InlineData("close", false)]
    [InlineData("cancel", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel-with-notice", true)]
    [InlineData("cancel-with-notice", false)]
    public async Task ChangedOrCancelledDiagnosticCannotPublishAlreadyCompletedResult(string change, bool succeeds)
    {
        using var handler = new DeferredModelHandler(succeeds);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var originalProfile = viewModel.SelectedProfile!;
        var context = new PausedSynchronizationContext();
        var diagnostic = StartDiagnostic(viewModel, context);

        handler.Release.TrySetResult();
        await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(diagnostic.IsCompleted);
        Assert.True(viewModel.IsTestingSelectedProfileConnection);
        Assert.True(viewModel.CanEditSelectedProfile);

        switch (change)
        {
            case "switch":
            case "switch-back":
                viewModel.SelectedProfile = viewModel.Profiles[1];
                if (change == "switch-back")
                    viewModel.SelectedProfile = originalProfile;
                break;
            case "edit":
            case "edit-back":
                var originalModel = originalProfile.Model;
                originalProfile.Model = "edited-model";
                if (change == "edit-back")
                    originalProfile.Model = originalModel;
                break;
            case "close":
                viewModel.Dispose();
                break;
            case "clear":
            case "clear-with-notice":
                if (change == "clear-with-notice")
                    viewModel.BackendSyncUrl = "https://changed.example.test/configuration";
                viewModel.SelectedProfile = null;
                break;
            case "cancel":
            case "cancel-with-notice":
                viewModel.TestSelectedProfileCommand.Execute(null);
                if (change == "cancel-with-notice")
                    viewModel.BackendSyncUrl = "https://changed.example.test/configuration";
                break;
            default:
                throw new InvalidOperationException("Unknown diagnostic transition.");
        }
        var noticeAfterChange = viewModel.SettingsStatusText;
        var testTextAfterChange = viewModel.SelectedProfileConnectionTestText;
        if (change != "close")
            Assert.DoesNotContain("Testing Profile A", noticeAfterChange, StringComparison.Ordinal);
        if (change == "clear")
            Assert.Contains("Model profile changed", noticeAfterChange, StringComparison.Ordinal);
        if (change is "clear-with-notice" or "cancel-with-notice")
            Assert.Contains("Backend sync settings changed", noticeAfterChange, StringComparison.Ordinal);

        context.RunPending();
        await diagnostic.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsTestingSelectedProfileConnection);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.DoesNotContain("Connected", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
        Assert.DoesNotContain("Connection failed", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
        Assert.DoesNotContain("Model test succeeded", viewModel.SettingsStatusText, StringComparison.Ordinal);
        if (change is "cancel" or "cancel-with-notice")
        {
            Assert.Equal("Connection test cancelled.", viewModel.SelectedProfileConnectionTestText);
            Assert.Equal(change == "cancel" ? viewModel.SelectedProfileConnectionTestText : noticeAfterChange, viewModel.SettingsStatusText);
        }
        else
        {
            Assert.Equal(noticeAfterChange, viewModel.SettingsStatusText);
            if (change == "close")
                Assert.Equal(testTextAfterChange, viewModel.SelectedProfileConnectionTestText);
            else if (change is "clear" or "clear-with-notice")
                Assert.StartsWith("Complete API key", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
            else
                Assert.Contains("current unsaved values", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
        }
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfileChangesCancelAnOutstandingRequestWithoutDisablingEditing(bool switchProfile)
    {
        using var handler = new DeferredModelHandler(succeeds: true);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var diagnostic = viewModel.TestSelectedProfileConnectionAsync();
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (switchProfile)
            viewModel.SelectedProfile = viewModel.Profiles[1];
        else
            viewModel.SelectedProfile!.BaseUrl = "https://changed.example.test/v1";

        await diagnostic.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(handler.RequestWasCancelled);
        Assert.False(viewModel.IsTestingSelectedProfileConnection);
        Assert.True(viewModel.CanEditSelectedProfile);
        Assert.Contains("current unsaved values", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
        Assert.DoesNotContain("cancelled", viewModel.SelectedProfileConnectionTestText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    private CopilotSettingsViewModel CreateViewModel(HttpClient client)
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "test-mcp-token",
            Profiles = new ObservableCollection<CopilotProfileConfig>
            {
                CreateProfile("a", "Profile A"),
                CreateProfile("b", "Profile B"),
            },
        };
        config.EnsureInitialized();
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json") };
        configHandler.Configs[typeof(CopilotConfig)] = config;
        var chatService = new CopilotChatService(client, maximumAttempts: 1, _ => TimeSpan.Zero, Task.Delay);
        return new CopilotSettingsViewModel(configHandler, new CopilotBackendSyncClient(client),
            new CopilotChatState { ActiveProfileId = config.Profiles[0].Id }, new CopilotModelConnectionDiagnostic(chatService));
    }

    private static CopilotProfileConfig CreateProfile(string id, string name) => new()
    {
        Id = id,
        Name = name,
        VendorType = CopilotVendorType.Custom,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "test-key",
        BaseUrl = "https://model.example.test/v1",
        Model = "test-model-" + id,
    };

    private static Task StartDiagnostic(CopilotSettingsViewModel viewModel, SynchronizationContext context)
    {
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            return viewModel.TestSelectedProfileConnectionAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    public void Dispose() => Directory.Delete(_rootDirectory, recursive: true);

    private sealed class PausedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public TaskCompletionSource CallbackPosted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            CallbackPosted.TrySetResult();
        }

        public void RunPending()
        {
            var previousContext = Current;
            try
            {
                SetSynchronizationContext(this);
                while (_callbacks.TryDequeue(out var item))
                    item.Callback(item.State);
            }
            finally
            {
                SetSynchronizationContext(previousContext);
            }
        }
    }

    private sealed class DeferredModelHandler(bool succeeds) : HttpMessageHandler
    {
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool RequestWasCancelled { get; private set; }
        public int RequestCount { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            RequestStarted.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                RequestWasCancelled = true;
                throw;
            }
            return new HttpResponseMessage(succeeds ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
            {
                Content = succeeds
                    ? new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
                    : new StringContent("{\"error\":{\"message\":\"Invalid test credentials\"}}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
