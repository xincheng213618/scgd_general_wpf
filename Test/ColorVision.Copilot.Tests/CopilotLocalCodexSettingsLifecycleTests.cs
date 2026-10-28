using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLocalCodexSettingsLifecycleTests
{
    [Theory]
    [InlineData("connection")]
    [InlineData("connection-snapshot")]
    [InlineData("account/read")]
    [InlineData("model/list")]
    [InlineData("model/list-snapshot")]
    public async Task ClosingSettingsRejectsLateSuccessfulDetectionAndDisposesItsServer(string stage)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new SettingsServer
        {
            InitialSignedIn = stage.EndsWith("-snapshot", StringComparison.Ordinal) ? true : null,
            DeferredMethod = stage == "model/list-snapshot" ? "model/list" : stage,
            Started = started,
            Release = release,
        };
        CancellationToken operationToken = default;
        using var settings = CreateSettings(() => ["synthetic-codex.exe"], async (_, token) =>
        {
            operationToken = token;
            if (stage is "connection" or "connection-snapshot")
            {
                started.TrySetResult();
                await release.Task;
            }
            return server;
        });
        var detection = settings.DetectLocalCodexAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var statusBeforeClose = settings.LocalCodexStatus;
        var notificationsAfterClose = new List<string?>();
        settings.Dispose();
        settings.PropertyChanged += (_, args) => notificationsAfterClose.Add(args.PropertyName);
        release.TrySetResult();
        await detection;

        Assert.True(operationToken.IsCancellationRequested);
        Assert.True(server.Disposed);
        Assert.False(settings.IsLocalCodexSignedIn);
        Assert.Empty(settings.LocalCodexModels);
        Assert.Equal(statusBeforeClose, settings.LocalCodexStatus);
        Assert.False(settings.IsCheckingLocalCodex);
        Assert.All(notificationsAfterClose, name => Assert.Equal(nameof(settings.IsCheckingLocalCodex), name));
        Assert.Equal(stage switch { "connection" or "connection-snapshot" => 0, "account/read" or "model/list-snapshot" => 1, _ => 2 }, server.Requests.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task EachDetectionPassesFreshCandidatesAndUsesOnlyItsOwnInitialAccountSnapshot(bool? initialSignedIn)
    {
        var first = new SettingsServer { InitialSignedIn = true, SignedIn = false };
        var second = new SettingsServer { InitialSignedIn = initialSignedIn, SignedIn = initialSignedIn != true };
        var servers = new Queue<SettingsServer>([first, second]);
        var scans = new List<IReadOnlyList<string>>();
        var connections = new List<IReadOnlyList<string>?>();
        using var settings = CreateSettings(() =>
        {
            IReadOnlyList<string> candidates = [$"synthetic-codex-{scans.Count + 1}.exe"];
            scans.Add(candidates);
            return candidates;
        }, (candidates, _) =>
        {
            connections.Add(candidates);
            return Task.FromResult<ICopilotCodexAppServer>(servers.Dequeue());
        });

        await settings.DetectLocalCodexAsync();
        Assert.True(settings.IsLocalCodexSignedIn);
        Assert.Equal(["model/list"], first.Requests);
        await settings.DetectLocalCodexAsync();

        Assert.Equal(2, scans.Count);
        Assert.Equal(2, connections.Count);
        Assert.Same(scans[0], connections[0]);
        Assert.Same(scans[1], connections[1]);
        Assert.NotSame(scans[0], scans[1]);
        Assert.NotEqual(scans[0][0], scans[1][0]);
        Assert.True(settings.HasLocalCodex);
        var signedIn = initialSignedIn ?? second.SignedIn;
        Assert.Equal(signedIn, settings.IsLocalCodexSignedIn);
        Assert.Equal(signedIn ? new[] { "synthetic-model" } : Array.Empty<string>(), settings.LocalCodexModels);
        Assert.Equal(initialSignedIn.HasValue ? 0 : 1, second.Requests.Count(method => method == "account/read"));
        Assert.Equal(signedIn ? 1 : 0, second.Requests.Count(method => method == "model/list"));
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    private static CopilotSettingsViewModel CreateSettings(
        Func<IReadOnlyList<string>> findCandidates,
        Func<IReadOnlyList<string>?, CancellationToken, Task<ICopilotCodexAppServer>> connect)
    {
        var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpEnabled = false, McpBearerToken = "test" };
        config.EnsureInitialized();
        var handler = new ConfigHandler();
        handler.Configs[typeof(CopilotConfig)] = config;
        return new CopilotSettingsViewModel(handler, new CopilotChatState(), findLocalCodexCandidates: findCandidates, connectLocalCodex: connect);
    }

    private sealed class SettingsServer : ICopilotCodexAppServer
    {
        public bool? InitialSignedIn { get; init; }
        public bool SignedIn { get; init; } = true;
        public bool FailModels { get; init; }
        public string? DeferredMethod { get; init; }
        public TaskCompletionSource? Started { get; init; }
        public TaskCompletionSource? Release { get; init; }
        public bool Disposed { get; private set; }
        public List<string> Requests { get; } = [];

        public async Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            Requests.Add(method);
            if (method == DeferredMethod)
            {
                Started!.TrySetResult();
                // A reply can already have completed when closing the window cancels its UI continuation.
                await Release!.Task;
            }
            if (method == "model/list" && FailModels)
                throw new InvalidOperationException("synthetic model failure");
            return JsonNode.Parse(method switch
            {
                "account/read" => SignedIn ? """{"account":{"type":"chatgpt"}}""" : """{"account":null}""",
                "model/list" => """{"data":[{"model":"synthetic-model"}]}""",
                _ => throw new InvalidOperationException("Unexpected method: " + method),
            })!;
        }

        public Task<JsonObject> ReadAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }
}
