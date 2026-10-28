using ColorVision.SocketProtocol;
using ProjectARVRPro.Services;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProjectARVRPro.Tests;

[CollectionDefinition(nameof(SocketControlTestGroup), DisableParallelization = true)]
public sealed class SocketControlTestGroup { }

[Collection(nameof(SocketControlTestGroup))]
public sealed class SocketControlTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task NonExecutingRequestsReplyNormallyWithoutStealingTheAsyncResultConnection()
    {
        using var stationA = await LoopbackPair.OpenAsync();
        using var stationB = await LoopbackPair.OpenAsync();
        SocketControl previous = SocketControl.Current;
        var control = new SocketControl { Stream = stationB.Stream };
        SocketControl.Current = control;
        try
        {
            // Both requests return their ordinary errors. Neither has an asynchronous result to own.
            var request = new SocketRequest { EventName = "SetProcessEnable", SerialNumber = "A", Params = "" };
            SocketResponse first = new SetProcessEnableSocket().Handle(stationA.Stream, request);
            SocketResponse repeated = new SetProcessEnableSocket().Handle(stationA.Stream, request);
            SocketResponse group = new SwitchGroupSocket().Handle(stationA.Stream,
                new SocketRequest { EventName = "SwitchGroup", Params = "" });
            Assert.Equal(-1, first.Code);
            Assert.Equal(first.Code, repeated.Code);
            Assert.Equal("A", first.SerialNumber);
            Assert.Equal(-1, group.Code);
            Assert.Same(stationB.Stream, control.Stream);
            control.Stream.Write(Encoding.UTF8.GetBytes("B-result;"));
            Assert.Equal("B-result;", await stationB.ReadAsync("B-result;"));
        }
        finally { SocketControl.Current = previous; }
    }

    [Fact]
    public async Task AnotherStationsRequestDoesNotInvalidateTheExistingImageSwitchAcknowledgement()
    {
        using var stationA = await LoopbackPair.OpenAsync();
        using var stationB = await LoopbackPair.OpenAsync();
        SocketControl previousControl = SocketControl.Current;
        var control = new SocketControl { Stream = stationB.Stream };
        SocketControl.Current = control;
        try
        {
            var sent = new TaskCompletionSource<SocketResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = new ExternalImageSwitchService(() => control.Stream, response => sent.TrySetResult(response));
            Task<ExternalImageSwitchResult> pending = service.ExecuteAsync("B", 1, 5000, 0, true, CancellationToken.None);
            SocketResponse message = await sent.Task.WaitAsync(Timeout);
            new SwitchGroupSocket().Handle(stationA.Stream, new SocketRequest { EventName = "SwitchGroup", Params = "" });
            Assert.True(service.TryComplete(stationB.Stream, Acknowledgement(message)));
            Assert.Equal(message.MsgID, (await pending.WaitAsync(Timeout)).MsgID);
        }
        finally { SocketControl.Current = previousControl; }
    }

    [Fact]
    public async Task DisconnectedImageSwitchCanBeExecutedAgainWithoutAStuckPendingRequest()
    {
        using var original = new MemoryStream();
        using var reconnected = new MemoryStream();
        Stream active = original;
        bool disconnect = true;
        ExternalImageSwitchService service = null!;
        service = new ExternalImageSwitchService(() => active, message =>
        {
            if (disconnect) { original.Dispose(); active = reconnected; }
            else Assert.True(service.TryComplete(active, Acknowledgement(message)));
        });
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync("B", 1, 5000, 0, true, CancellationToken.None));
        disconnect = false;
        Assert.Equal("B", (await service.ExecuteAsync("B", 1, 5000, 0, true, CancellationToken.None)).SerialNumber);
    }

    private static SocketRequest Acknowledgement(SocketResponse message) => new()
    {
        EventName = ExternalImageSwitchService.CompletionEvent, MsgID = message.MsgID, SerialNumber = message.SerialNumber
    };

    private sealed class LoopbackPair(TcpClient peer, TcpClient server) : IDisposable
    {
        internal NetworkStream Stream { get; } = server.GetStream();

        internal static async Task<LoopbackPair> OpenAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var peer = new TcpClient();
            try
            {
                await peer.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port).WaitAsync(Timeout);
                return new(peer, await listener.AcceptTcpClientAsync().WaitAsync(Timeout));
            }
            catch { peer.Dispose(); throw; }
            finally { listener.Stop(); }
        }

        internal async Task<string> ReadAsync(string expected)
        {
            byte[] bytes = new byte[Encoding.UTF8.GetByteCount(expected)];
            await peer.GetStream().ReadExactlyAsync(bytes).AsTask().WaitAsync(Timeout);
            return Encoding.UTF8.GetString(bytes);
        }

        public void Dispose() { server.Dispose(); peer.Dispose(); }
    }
}
