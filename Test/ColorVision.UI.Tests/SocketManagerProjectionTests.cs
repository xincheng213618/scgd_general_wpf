using ColorVision.SocketProtocol;
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

    private static SocketManager CreateProjectionManager(bool isServerEnabled)
    {
        var manager = (SocketManager)RuntimeHelpers.GetUninitializedObject(typeof(SocketManager));
        manager.Config = new SocketConfig { IsServerEnabled = isServerEnabled };
        return manager;
    }
}
