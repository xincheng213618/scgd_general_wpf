using ProjectARVRPro.SemiAuto.GECS;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ProjectARVRPro.SemiAuto.Tests;

public class GecsClientTests
{
    [Fact]
    public async Task SendCommandWaitsThroughProcessingAndAcceptsMatchingSuccess()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = RunServerAsync(listener, "PG,01,PATTERN,INDEX,1", "ALIVE", "PG,01,PATTERN,processing ...", "PG,01,PATTERN,INDEX,1,END,OK");
        using var client = new GecsClient();
        var logs = new ConcurrentQueue<string>();
        client.Log += logs.Enqueue;
        await client.ConnectAsync("127.0.0.1", port);

        GecsCommandResult result = await client.SendCommandAsync(
            "PG,01,PATTERN,INDEX,1",
            ",PATTERN,INDEX,END,OK",
            0xFF,
            TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal("PG,01,PATTERN,INDEX,1,END,OK", result.ResponseText);
        Assert.DoesNotContain(logs, message => message.Contains("ALIVE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, message => message.Contains("Sent [Network=255/0xFF, Length=0015 (21 bytes), Packet=28 bytes]", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.StartsWith("Sent HEX: 02 FF 30 30 31 35", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Equals("Result [OK]: PG,01,PATTERN,INDEX,1,END,OK", StringComparison.Ordinal));
        await server;
    }

    [Fact]
    public async Task OrderedSuccessFieldsStillRequireTheResponseToEchoTheSentCommand()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = RunServerAsync(listener, "PG,01,PATTERN,INDEX,1", "PG,01,PATTERN,INDEX,2,END,OK");
        using var client = new GecsClient();
        await client.ConnectAsync("127.0.0.1", port);

        GecsCommandResult result = await client.SendCommandAsync(
            "PG,01,PATTERN,INDEX,1",
            ",PATTERN,INDEX,END,OK",
            0xFF,
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        await server;
    }

    [Fact]
    public async Task SendCommandRejectsNgWithoutWaitingForSuccessText()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = RunServerAsync(listener, "PG,01,POWER,ON", "PG,01,POWER,ON,END,NG,E101");
        using var client = new GecsClient();
        await client.ConnectAsync("127.0.0.1", port);

        GecsCommandResult result = await client.SendCommandAsync(
            "PG,01,POWER,ON",
            ",POWER,ON,END,OK",
            0xFF,
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Contains("END,NG", result.ErrorMessage);
        Assert.Contains("END,NG", result.ResponseText);
        await server;
    }

    [Fact]
    public async Task HeartbeatIsSentWithoutAddingAliveLogEntries()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = RunServerAsync(listener, "ALIVE");
        using var client = new GecsClient();
        var logs = new ConcurrentQueue<string>();
        client.Log += logs.Enqueue;
        await client.ConnectAsync("127.0.0.1", port);

        Assert.True(await client.TrySendHeartbeatAsync(0xFF));

        await server;
        Assert.DoesNotContain(logs, message => message.Contains("ALIVE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CommandPreviewIncludesResolvedCommandLengthAndHexWithoutSending()
    {
        using var client = new GecsClient();
        var logs = new ConcurrentQueue<string>();
        client.Log += logs.Enqueue;

        client.LogCommandPreview("PG,01,PATTERN,INDEX,1", 0xFF);

        Assert.Contains(logs, message => message.Contains("Prepared (not sent) [Network=255/0xFF, Length=0015 (21 bytes), Packet=28 bytes]: PG,01,PATTERN,INDEX,1", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.StartsWith("Prepared (not sent) HEX: 02 FF 30 30 31 35", StringComparison.Ordinal));
    }

    private static async Task RunServerAsync(TcpListener listener, string expectedCommand, params string[] responses)
    {
        using TcpClient serverClient = await listener.AcceptTcpClientAsync();
        using NetworkStream stream = serverClient.GetStream();
        var reader = new GecsFrameReader(stream);
        GecsFrame request = await reader.ReadFrameAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(request);
        Assert.Equal(0xFF, request.NetworkNumber);
        Assert.Equal(expectedCommand, request.MessageText);

        foreach (string response in responses)
            await WriteFrameAsync(stream, response);
    }

    private static async Task WriteFrameAsync(NetworkStream stream, string text)
    {
        byte[] packet = GecsPacketCodec.BuildPacket(text, 0xFF);
        int split = Math.Min(4, packet.Length);
        await stream.WriteAsync(packet.AsMemory(0, split));
        await stream.FlushAsync();
        await stream.WriteAsync(packet.AsMemory(split));
        await stream.FlushAsync();
    }
}
