using ColorVision.SocketProtocol;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class SocketManagerProjectionTests
{
    [Fact]
    public void DisabledConfigurationDoesNotHideStopFailure()
    {
        SocketManager manager = CreateProjectionManager(isServerEnabled: false);
        var failure = new InvalidOperationException("simulated stop failure");

        manager.ApplyServerTransitionCore(new SocketServerTransition(
            1,
            SocketServerState.Error,
            Exception: failure,
            FailureStage: SocketServerFailureStage.Stop));

        Assert.Equal(SocketServerState.Error, manager.ServerState);
        Assert.Equal(ColorVision.SocketProtocol.Properties.Resources.OpenFailed, manager.ServerStateText);
        Assert.Equal(ColorVision.SocketProtocol.Properties.Resources.OpenFailed, manager.OpenStatusText);
        Assert.True(manager.HasLastError);
        Assert.Contains(failure.Message, manager.LastErrorDisplay, StringComparison.Ordinal);
    }

    [Fact]
    public void OlderTransitionCannotOverwriteNewerProjection()
    {
        SocketManager manager = CreateProjectionManager(isServerEnabled: true);
        var settings = new SocketServerSettings(
            "127.0.0.1",
            6666,
            4096,
            SocketPhraseType.Json,
            true);

        manager.ApplyServerTransitionCore(new SocketServerTransition(2, SocketServerState.Running, settings));
        manager.ApplyServerTransitionCore(new SocketServerTransition(
            1,
            SocketServerState.Error,
            settings,
            new InvalidOperationException("stale failure"),
            SocketServerFailureStage.Start));

        Assert.Equal(SocketServerState.Running, manager.ServerState);
        Assert.True(manager.IsConnect);
        Assert.False(manager.HasLastError);
        Assert.Equal(string.Empty, manager.LastErrorMessage);
    }

    [Fact]
    public async Task BackgroundLifecycleTransitionIsProjectedOnWpfDispatcher()
    {
        int uiThreadId = WpfTestHost.Invoke(() => Environment.CurrentManagedThreadId);
        var work = new ConcurrentQueue<Action>();
        var tracker = new SocketWorkerTracker();
        SocketManager manager = CreateRuntimeManager(work, tracker);
        var projected = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SocketManager.ServerState)
                && manager.ServerState == SocketServerState.Starting)
                projected.TrySetResult(Environment.CurrentManagedThreadId);
        };

        await Task.Run(manager.StartServer);
        int projectionThreadId = await projected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(uiThreadId, projectionThreadId);
        Assert.Equal(SocketServerState.Starting, manager.ServerState);
        Assert.True(work.TryDequeue(out Action? worker));
        Assert.Empty(work);
        worker();
        Assert.True(manager.Shutdown(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    private static SocketManager CreateProjectionManager(bool isServerEnabled)
    {
        var manager = (SocketManager)RuntimeHelpers.GetUninitializedObject(typeof(SocketManager));
        manager.Config = new SocketConfig { IsServerEnabled = isServerEnabled };
        return manager;
    }

    private static SocketManager CreateRuntimeManager(
        ConcurrentQueue<Action> work,
        SocketWorkerTracker tracker)
    {
        var config = new SocketConfig
        {
            IPAddress = "127.0.0.1",
            ServerPort = 0,
            SocketBufferSize = 4096,
            SocketPhraseType = SocketPhraseType.Text,
            IsServerEnabled = true
        };
        var messageManager = (SocketMessageManager)RuntimeHelpers.GetUninitializedObject(typeof(SocketMessageManager));
        var jsonDispatcher = (SocketJsonDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketJsonDispatcher));
        var textDispatcher = (SocketTextDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketTextDispatcher));
        return new SocketManager(
            config,
            new ThrowingListenerFactory(),
            work.Enqueue,
            tracker,
            jsonDispatcher,
            textDispatcher,
            messageManager,
            refreshNetworkAccessStatus: false);
    }

    private sealed class ThrowingListenerFactory : ISocketServerListenerFactory
    {
        public ISocketServerListener Create(SocketServerSettings settings) =>
            throw new InvalidOperationException("simulated listener creation failure");
    }
}
