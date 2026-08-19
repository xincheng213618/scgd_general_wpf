using ColorVision.Copilot.Mcp;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMcpServerLifecycleTests
{
    [Fact]
    public async Task ShutdownCancelsActiveClientsAndPermanentlyStopsListener()
    {
        var server = new CopilotMcpServer();
        var port = GetAvailablePort();
        server.ApplySettings(new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = "test-token",
        });
        using var client = new TcpClient();

        await client.ConnectAsync(IPAddress.Loopback, port);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(
            "POST /mcp HTTP/1.1\r\nHost: localhost\r\n"));
        await WaitForActiveClientAsync(server);

        Assert.True(await server.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(server.IsRunning);
        Assert.Equal(0, server.ActiveClientCount);

        server.ApplySettings(new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = "test-token",
        });
        Assert.False(server.IsRunning);
        Assert.True(await server.ShutdownAsync());
    }

    private static async Task WaitForActiveClientAsync(CopilotMcpServer server)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (server.ActiveClientCount == 0 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Equal(1, server.ActiveClientCount);
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
