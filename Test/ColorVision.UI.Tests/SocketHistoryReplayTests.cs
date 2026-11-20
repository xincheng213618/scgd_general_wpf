using ColorVision.SocketProtocol;
using System.Net;
using System.Net.Sockets;

namespace ColorVision.UI.Tests;

public sealed class SocketHistoryReplayTests
{
    [Fact]
    public async Task LegacyRecordMatchesOnlyAUniqueFullRemoteEndpoint()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var other = await LoopbackPeer.ConnectAsync(listener);
        using var original = await LoopbackPeer.ConnectAsync(listener);
        SocketServerClient originalRoute = CreateRoute(original.Accepted);
        SocketServerClient otherRoute = CreateRoute(other.Accepted);
        var remote = (IPEndPoint)original.Accepted.Client.RemoteEndPoint!;

        Assert.Same(originalRoute, SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = remote.ToString() }, [otherRoute, originalRoute]));
        Assert.Null(SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = $"previous client {remote}" }, [otherRoute, originalRoute]));
        Assert.Null(SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = remote.Address.ToString() }, [otherRoute, originalRoute]));
        Assert.Null(SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.2"), remote.Port).ToString() }, [otherRoute, originalRoute]));
        int differentPort = remote.Port == 1 ? 2 : 1;
        Assert.Null(SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = new IPEndPoint(remote.Address, differentPort).ToString() }, [originalRoute]));
        Assert.Null(SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = listener.LocalEndpoint.ToString()! }, [otherRoute, originalRoute]));
        Assert.Null(SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = null! }, [otherRoute, originalRoute]));

        SocketServerClient ambiguousRoute = CreateRoute(original.Accepted, version: 2);
        Assert.Null(SocketManager.FindReplayTarget(
            new SocketMessage { ClientEndPoint = remote.ToString() }, [otherRoute, ambiguousRoute, originalRoute]));
    }

    [Fact]
    public async Task LegacyBracketedIPv6EndpointMatchesItsPeer()
    {
        if (!Socket.OSSupportsIPv6)
            return;

        using var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
        listener.Start();
        using var peer = await LoopbackPeer.ConnectAsync(listener);
        SocketServerClient route = CreateRoute(peer.Accepted);
        string remote = peer.Accepted.Client.RemoteEndPoint!.ToString()!;

        Assert.StartsWith("[::1]:", remote, StringComparison.Ordinal);
        Assert.Same(route, SocketManager.FindReplayTarget(new SocketMessage { ClientEndPoint = remote }, [route]));
    }

    [Fact]
    public async Task RecordedConnectionIdentityRejectsAReplacementUsingTheSameAddress()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var original = await LoopbackPeer.ConnectAsync(listener);
        SocketServerClient originalRoute = CreateRoute(original.Accepted);
        // Model endpoint reuse without trying to reuse a kernel port while the first socket is open.
        SocketServerClient replacementRoute = CreateRoute(original.Accepted, version: 2);
        var message = new SocketMessage
        {
            ClientEndPoint = original.Accepted.Client.RemoteEndPoint!.ToString()!,
            ClientConnectionId = originalRoute.ConnectionId,
        };

        Assert.NotEqual(originalRoute.ConnectionId, replacementRoute.ConnectionId);
        Assert.Same(originalRoute, SocketManager.FindReplayTarget(message, [replacementRoute, originalRoute]));
        Assert.Null(SocketManager.FindReplayTarget(message, [replacementRoute]));

        Assert.True(originalRoute.TryMarkClosed());
        Assert.Null(SocketManager.FindReplayTarget(message, [originalRoute, replacementRoute]));
    }

    [Fact]
    public async Task DisposedOrStoppingOriginalDoesNotRedirectReplayToAnotherClient()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var other = await LoopbackPeer.ConnectAsync(listener);
        using var original = await LoopbackPeer.ConnectAsync(listener);
        SocketServerClient otherRoute = CreateRoute(other.Accepted);
        SocketServerClient originalRoute = CreateRoute(original.Accepted);
        var message = new SocketMessage { ClientEndPoint = original.Accepted.Client.RemoteEndPoint!.ToString()! };

        originalRoute.Session.RequestStop();
        Assert.Null(SocketManager.FindReplayTarget(message, [otherRoute, originalRoute]));

        SocketServerClient disposedRoute = CreateRoute(original.Accepted, version: 2);
        original.Accepted.Dispose();
        Assert.Null(SocketManager.FindReplayTarget(message, [otherRoute, disposedRoute]));
        Assert.Null(SocketManager.FindReplayTarget(message, [otherRoute]));
    }

    private static SocketServerClient CreateRoute(TcpClient accepted, long version = 1)
    {
        var listener = (IPEndPoint)accepted.Client.LocalEndPoint!;
        var settings = new SocketServerSettings(listener.Address.ToString(), listener.Port, 4096, SocketPhraseType.Text, true);
        var session = new SocketServerSession(version, settings);
        var route = new SocketServerClient(session, accepted);
        Assert.True(session.TryRegisterClient(route));
        return route;
    }

    private sealed class LoopbackPeer(TcpClient peer, TcpClient accepted) : IDisposable
    {
        public TcpClient Accepted { get; } = accepted;

        public static async Task<LoopbackPeer> ConnectAsync(TcpListener listener)
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var peer = new TcpClient(endpoint.AddressFamily);
            try
            {
                await peer.ConnectAsync(endpoint.Address, endpoint.Port, deadline.Token);
                return new LoopbackPeer(peer, await listener.AcceptTcpClientAsync(deadline.Token));
            }
            catch
            {
                peer.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Accepted.Dispose();
            peer.Dispose();
        }
    }
}
