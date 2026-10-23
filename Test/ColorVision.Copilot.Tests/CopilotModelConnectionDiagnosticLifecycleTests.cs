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
        var diagnostic = StartDiagnostic(viewModel.TestSelectedProfileConnectionAsync, context);

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
        else
        {
            Assert.Equal(viewModel.SelectedProfileConnectionTestText, viewModel.SettingsStatusText);
        }
        Assert.Equal(1, handler.RequestCount);
        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("test-model-a", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal(128, payload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0, payload.RootElement.GetProperty("temperature").GetDouble());
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task DiagnosticResultPreservesNewerSettingsNotice(bool succeeds, bool invalidPort)
    {
        using var handler = new DeferredModelHandler(succeeds);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var profile = viewModel.SelectedProfile;
        var context = new PausedSynchronizationContext();
        var diagnostic = StartDiagnostic(viewModel.TestSelectedProfileConnectionAsync, context);

        handler.Release.TrySetResult();
        await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(diagnostic.IsCompleted);
        if (invalidPort)
        {
            viewModel.McpPortText = "invalid-port";
            Assert.False(viewModel.Save());
            Assert.False(viewModel.CanSaveSettings);
        }
        else
        {
            viewModel.AgentTimeoutSeconds++;
            Assert.True(viewModel.HasUnsavedSettings);
        }
        var notice = viewModel.SettingsStatusText;
        Assert.DoesNotContain("Testing Profile A", notice, StringComparison.Ordinal);

        context.RunPending();
        await diagnostic.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(profile, viewModel.SelectedProfile);
        Assert.StartsWith(succeeds ? "Connected in " : "Connection failed in ", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
        Assert.Equal(notice, viewModel.SettingsStatusText);
        Assert.False(viewModel.IsTestingSelectedProfileConnection);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.False(handler.RequestWasCancelled);
        Assert.Equal(1, handler.RequestCount);
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
        var diagnostic = StartDiagnostic(viewModel.TestSelectedProfileConnectionAsync, context);

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
                    viewModel.AgentTimeoutSeconds++;
                viewModel.SelectedProfile = null;
                break;
            case "cancel":
            case "cancel-with-notice":
                viewModel.TestSelectedProfileCommand.Execute(null);
                if (change == "cancel-with-notice")
                    viewModel.AgentTimeoutSeconds++;
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
            Assert.Contains("Agent timeout changed", noticeAfterChange, StringComparison.Ordinal);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestedModelWaitsForPreviousCancellationCleanup(bool selectBeforeRequest)
    {
        using var handler = new SequentialModelHandler();
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var first = viewModel.TestSelectedProfileConnectionAsync();
        await handler.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var target = viewModel.Profiles[1];
        if (selectBeforeRequest)
            viewModel.SelectedProfile = target;

        var requested = viewModel.TestProfileConnectionAsync(target);
        try
        {
            await handler.FirstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(requested.IsCompleted);
            Assert.Equal(1, handler.RequestCount);
            Assert.True(viewModel.IsTestingSelectedProfileConnection);
            Assert.Same(target, viewModel.SelectedProfile);
            Assert.Contains("随后测试 Profile B", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);

            handler.ReleaseFirstCleanup.TrySetResult();
            await handler.NextStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "test-model-a", "test-model-b" }, handler.Models.ToArray());
            Assert.True(viewModel.IsTestingSelectedProfileConnection);
            Assert.Equal(1, handler.MaximumConcurrentRequests);
            handler.ReleaseNext.TrySetResult();
            await Task.WhenAll(first, requested).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.StartsWith("Connected in ", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
            Assert.Contains("Model test succeeded for Profile B.", viewModel.SettingsStatusText, StringComparison.Ordinal);
            Assert.False(viewModel.IsTestingSelectedProfileConnection);
        }
        finally
        {
            handler.ReleaseFirstCleanup.TrySetResult();
            handler.ReleaseNext.TrySetResult();
        }
    }

    [Fact]
    public async Task LatestRequestedModelReplacesTheWaitingRequest()
    {
        using var handler = new SequentialModelHandler();
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var first = viewModel.TestSelectedProfileConnectionAsync();
        await handler.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var superseded = viewModel.TestProfileConnectionAsync(viewModel.Profiles[1]);
        var latestModel = CreateProfile("c", "Profile C");
        viewModel.Profiles.Add(latestModel);
        var latest = viewModel.TestProfileConnectionAsync(latestModel);
        try
        {
            await handler.FirstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, handler.RequestCount);
            handler.ReleaseFirstCleanup.TrySetResult();
            await handler.NextStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "test-model-a", "test-model-c" }, handler.Models.ToArray());
            Assert.Equal(1, handler.MaximumConcurrentRequests);
            handler.ReleaseNext.TrySetResult();
            await Task.WhenAll(first, superseded, latest).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Same(latestModel, viewModel.SelectedProfile);
            Assert.Contains("Model test succeeded for Profile C.", viewModel.SettingsStatusText, StringComparison.Ordinal);
        }
        finally
        {
            handler.ReleaseFirstCleanup.TrySetResult();
            handler.ReleaseNext.TrySetResult();
        }
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("switch-back")]
    [InlineData("edit")]
    [InlineData("remove")]
    [InlineData("cancel")]
    [InlineData("repeat-row")]
    [InlineData("repeat-row-after-cleanup")]
    [InlineData("close")]
    public async Task WaitingDiagnosticIsRevokedByFurtherUserChanges(string change)
    {
        using var handler = new SequentialModelHandler();
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var first = viewModel.TestSelectedProfileConnectionAsync();
        await handler.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var target = viewModel.Profiles[1];
        var context = change == "repeat-row-after-cleanup" ? new PausedSynchronizationContext() : null;
        var requested = context == null ? viewModel.TestProfileConnectionAsync(target)
            : StartDiagnostic(() => viewModel.TestProfileConnectionAsync(target), context);
        try
        {
            await handler.FirstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (context != null)
            {
                handler.ReleaseFirstCleanup.TrySetResult();
                await first.WaitAsync(TimeSpan.FromSeconds(5));
                await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            switch (change)
            {
                case "switch":
                case "switch-back":
                    viewModel.SelectedProfile = viewModel.Profiles[0];
                    if (change == "switch-back")
                        viewModel.SelectedProfile = target;
                    break;
                case "edit":
                    target.Model = "edited-model";
                    break;
                case "remove":
                    viewModel.Profiles.Remove(target);
                    break;
                case "cancel":
                    viewModel.TestSelectedProfileCommand.Execute(null);
                    break;
                case "repeat-row":
                case "repeat-row-after-cleanup":
                    await viewModel.TestProfileConnectionAsync(target);
                    break;
                case "close":
                    viewModel.Dispose();
                    break;
                default:
                    throw new InvalidOperationException("Unknown waiting diagnostic transition.");
            }

            var settingsNotice = viewModel.SettingsStatusText;
            var testText = viewModel.SelectedProfileConnectionTestText;
            handler.ReleaseFirstCleanup.TrySetResult();
            context?.RunPending();
            await Task.WhenAll(first, requested).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new[] { "test-model-a" }, handler.Models.ToArray());
            Assert.Equal(1, handler.MaximumConcurrentRequests);
            Assert.False(viewModel.IsTestingSelectedProfileConnection);
            Assert.DoesNotContain("Connected", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);
            if (change is "cancel" or "repeat-row" or "repeat-row-after-cleanup")
            {
                Assert.Equal("Connection test cancelled.", viewModel.SelectedProfileConnectionTestText);
                Assert.Equal(viewModel.SelectedProfileConnectionTestText, viewModel.SettingsStatusText);
            }
            else
            {
                Assert.Equal(settingsNotice, viewModel.SettingsStatusText);
            }
            if (change == "close")
                Assert.Equal(testText, viewModel.SelectedProfileConnectionTestText);
        }
        finally
        {
            handler.ReleaseFirstCleanup.TrySetResult();
            handler.ReleaseNext.TrySetResult();
        }
    }

    [Fact]
    public async Task ClickingTheRunningModelCancelsWithoutRestartingIt()
    {
        using var handler = new SequentialModelHandler();
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var first = viewModel.TestProfileConnectionAsync(viewModel.SelectedProfile!);
        await handler.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await viewModel.TestProfileConnectionAsync(viewModel.SelectedProfile!);
            await handler.FirstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            handler.ReleaseFirstCleanup.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, handler.RequestCount);
            Assert.Equal("Connection test cancelled.", viewModel.SelectedProfileConnectionTestText);
            Assert.False(viewModel.IsTestingSelectedProfileConnection);
        }
        finally
        {
            handler.ReleaseFirstCleanup.TrySetResult();
            handler.ReleaseNext.TrySetResult();
        }
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
        return new CopilotSettingsViewModel(configHandler,
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

    private static Task StartDiagnostic(Func<Task> diagnostic, SynchronizationContext context)
    {
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            return diagnostic();
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

    private sealed class SequentialModelHandler : HttpMessageHandler
    {
        private readonly object _concurrencyLock = new();
        private int _activeRequests;
        private int _requestCount;
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstCleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NextStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseNext { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<string> Models { get; } = new();
        public int RequestCount => Volatile.Read(ref _requestCount);
        public int MaximumConcurrentRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref _requestCount);
            lock (_concurrencyLock)
                MaximumConcurrentRequests = Math.Max(MaximumConcurrentRequests, ++_activeRequests);
            try
            {
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                Models.Enqueue(payload.RootElement.GetProperty("model").GetString()!);
                if (requestNumber == 1)
                {
                    FirstStarted.TrySetResult();
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        FirstCancelled.TrySetResult();
                        // Keep cancellation cleanup in flight until the test admits it.
                        await ReleaseFirstCleanup.Task.ConfigureAwait(false);
                        throw;
                    }
                }

                NextStarted.TrySetResult();
                await ReleaseNext.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream"),
                };
            }
            finally
            {
                lock (_concurrencyLock)
                    _activeRequests--;
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
