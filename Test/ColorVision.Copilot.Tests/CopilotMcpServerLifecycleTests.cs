using ColorVision.Copilot.Mcp;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMcpServerLifecycleTests
{
    [Theory]
    [InlineData(false, "test-token")]
    [InlineData(true, "")]
    public async Task InactiveLifecycleDoesNotCreateRequestHandler(bool enabled, string token)
    {
        using var server = new CopilotMcpServer();
        var handler = GetRequestHandler(server);
        Assert.False(handler.IsValueCreated);

        server.ApplySettings(new CopilotMcpRuntimeSettings { Enabled = enabled, BearerToken = token });
        Assert.False(server.IsRunning);
        Assert.False(handler.IsValueCreated);

        server.Stop();
        Assert.False(handler.IsValueCreated);
        Assert.True(await server.ShutdownAsync());
        server.ApplySettings(new CopilotMcpRuntimeSettings { Enabled = true, BearerToken = "test-token" });
        server.Dispose();
        Assert.False(server.IsRunning);
        Assert.False(handler.IsValueCreated);
    }

    [Fact]
    public async Task TokenRotationAndRestartReuseHandlerAndInvalidateExistingSessions()
    {
        using var server = new CopilotMcpServer();
        var port = GetAvailablePort();
        var handler = GetRequestHandler(server);
        server.ApplySettings(CreateSettings("test-token"));
        Assert.True(server.IsRunning);
        Assert.True(handler.IsValueCreated);
        var createdHandler = handler.Value;

        var sessionStore = Assert.IsType<CopilotMcpClientSessionStore>(
            typeof(CopilotMcpRequestHandler).GetField("_sessionStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(createdHandler));
        Assert.True(sessionStore.TryCreate("tcp://local", out var session));
        server.ApplySettings(CreateSettings("test-token"));
        Assert.True(sessionStore.TryResolve(session!.SessionId, "tcp://local", out _));

        server.ApplySettings(CreateSettings("rotated-token"));
        Assert.Same(createdHandler, handler.Value);
        Assert.False(sessionStore.TryResolve(session.SessionId, "tcp://local", out _));
        Assert.True(sessionStore.TryCreate("tcp://local", out session));
        server.Stop();
        Assert.False(sessionStore.TryResolve(session!.SessionId, "tcp://local", out _));

        server.ApplySettings(CreateSettings("rotated-token"));
        Assert.True(server.IsRunning);
        Assert.Same(createdHandler, handler.Value);
        Assert.True(await server.ShutdownAsync());

        CopilotMcpRuntimeSettings CreateSettings(string bearerToken) => new()
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = bearerToken,
        };
    }

    [Fact]
    public async Task ShutdownCancelsActiveClientsAndPermanentlyStopsListener()
    {
        using var server = new CopilotMcpServer();
        var handler = GetRequestHandler(server);
        Assert.False(handler.IsValueCreated);
        var port = GetAvailablePort();
        server.ApplySettings(new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = "test-token",
        });
        Assert.True(handler.IsValueCreated);
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

    private static Lazy<CopilotMcpRequestHandler> GetRequestHandler(CopilotMcpServer server) =>
        Assert.IsType<Lazy<CopilotMcpRequestHandler>>(
            typeof(CopilotMcpServer).GetField("_requestHandler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(server));

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
