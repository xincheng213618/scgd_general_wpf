using ProjectARVRPro.SemiAuto.GECS;
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
        Task server = RunServerAsync(listener, "PG,01,PATTERN,INDEX,1", "PG,01,PATTERN,processing ...", "PG,01,PATTERN,INDEX,END,OK");
        using var client = new GecsClient();
        await client.ConnectAsync("127.0.0.1", port);

        GecsCommandResult result = await client.SendCommandAsync(
            "PG,01,PATTERN,INDEX,1",
            ",PATTERN,INDEX,END,OK",
            0xFF,
            TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal("PG,01,PATTERN,INDEX,END,OK", result.ResponseText);
        await server;
    }

    [Fact]
    public async Task SendCommandRejectsNgWithoutWaitingForSuccessText()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = RunServerAsync(listener, "PG,01,POWER,ON", "PG,01,POWER,ON,END,NG,E101", null);
        using var client = new GecsClient();
        await client.ConnectAsync("127.0.0.1", port);

        GecsCommandResult result = await client.SendCommandAsync(
            "PG,01,POWER,ON",
            ",POWER,ON,END,OK",
            0xFF,
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Contains("END,NG", result.ErrorMessage);
        await server;
    }

    private static async Task RunServerAsync(TcpListener listener, string expectedCommand, string firstResponse, string secondResponse)
    {
        using TcpClient serverClient = await listener.AcceptTcpClientAsync();
        using NetworkStream stream = serverClient.GetStream();
        var reader = new GecsFrameReader(stream);
        GecsFrame request = await reader.ReadFrameAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(request);
        Assert.Equal(0xFF, request.NetworkNumber);
        Assert.Equal(expectedCommand, request.MessageText);

        await WriteFrameAsync(stream, firstResponse);
        if (!string.IsNullOrEmpty(secondResponse))
            await WriteFrameAsync(stream, secondResponse);
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
