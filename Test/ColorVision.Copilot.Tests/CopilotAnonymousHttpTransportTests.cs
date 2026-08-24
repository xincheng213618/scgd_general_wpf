using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAnonymousHttpTransportTests
{
    [Fact]
    public void DocumentationHandlerIsStatelessAndDoesNotFollowRedirects()
    {
        using var handler = CopilotDocsToolSupport.CreateHttpHandler();

        Assert.False(handler.UseCookies);
        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void WebSearchHandlerIsStatelessAndDoesNotFollowRedirects()
    {
        using var handler = CopilotWebSearchCapability.CreateHttpHandler();

        Assert.False(handler.UseCookies);
        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void WebPageHandlerIsStatelessAndKeepsRedirectAndProxyGuards()
    {
        using var handler = CopilotWebPageToolSupport.CreateHttpHandler();
        using var request = CopilotWebPageToolSupport.CreateWebPageRequestMessage(
            new Uri("https://example.test/resource"));

        Assert.False(handler.UseCookies);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.Equal(TimeSpan.Zero, handler.PooledConnectionIdleTimeout);
        Assert.Equal(TimeSpan.Zero, handler.PooledConnectionLifetime);
        Assert.NotNull(handler.ConnectCallback);
        Assert.Equal(HttpVersion.Version20, request.Version);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, request.VersionPolicy);
        Assert.NotEqual(true, request.Headers.ConnectionClose);
    }

    [Fact]
    public async Task WebPageRedirectUsesAndDisposesAnIndependentTransportForEveryRequest()
    {
        var responseFactories = new Queue<Func<HttpResponseMessage>>(
        [
            static () => new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("/final", UriKind.Relative) },
            },
            static () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<html><head><title>Transport test</title></head><body><main>Final content</main></body></html>",
                    Encoding.UTF8,
                    "text/html"),
            },
        ]);
        var requests = new List<(Version Version, HttpVersionPolicy Policy, bool? ConnectionClose)>();
        var activeHandlersAtCreation = new List<int>();
        var createdHandlers = 0;
        var disposedHandlers = 0;
        var activeHandlers = 0;

        var page = await CopilotWebPageToolSupport.LoadWebPageContentAsync(
            "https://public.test/start",
            static (_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") }),
            () =>
            {
                activeHandlersAtCreation.Add(Volatile.Read(ref activeHandlers));
                Interlocked.Increment(ref createdHandlers);
                Interlocked.Increment(ref activeHandlers);
                var createResponse = responseFactories.Dequeue();
                return new SingleResponseHandler(
                    createResponse,
                    request => requests.Add((request.Version, request.VersionPolicy, request.Headers.ConnectionClose)),
                    () =>
                    {
                        Interlocked.Decrement(ref activeHandlers);
                        Interlocked.Increment(ref disposedHandlers);
                    });
            },
            static () => string.Empty,
            CancellationToken.None);

        Assert.Equal("https://public.test/final", page.Url);
        Assert.Equal("Transport test", page.Title);
        Assert.Equal(2, createdHandlers);
        Assert.Equal(2, disposedHandlers);
        Assert.Equal(0, activeHandlers);
        Assert.Collection(
            activeHandlersAtCreation,
            activeHandlers => Assert.Equal(0, activeHandlers),
            activeHandlers => Assert.Equal(0, activeHandlers));
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Equal(HttpVersion.Version20, request.Version);
            Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, request.Policy);
            Assert.NotEqual(true, request.ConnectionClose);
        });
    }

    [Fact]
    public async Task WebPageRedirectToPrivateDnsTargetIsRejectedBeforeCreatingAnotherTransport()
    {
        var resolvedHosts = new List<string>();
        var createdHandlers = 0;
        var disposedHandlers = 0;
        var sentRequests = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.LoadWebPageContentAsync(
                "https://public.test/start",
                (host, _) =>
                {
                    resolvedHosts.Add(host);
                    return Task.FromResult(new[]
                    {
                        string.Equals(host, "rebind.test", StringComparison.OrdinalIgnoreCase)
                            ? IPAddress.Loopback
                            : IPAddress.Parse("93.184.216.34"),
                    });
                },
                () =>
                {
                    Interlocked.Increment(ref createdHandlers);
                    return new SingleResponseHandler(
                        static () => new HttpResponseMessage(HttpStatusCode.Redirect)
                        {
                            Headers = { Location = new Uri("https://rebind.test/private") },
                        },
                        _ => Interlocked.Increment(ref sentRequests),
                        () => Interlocked.Increment(ref disposedHandlers));
                },
                static () => string.Empty,
                CancellationToken.None));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Collection(
            resolvedHosts,
            host => Assert.Equal("public.test", host),
            host => Assert.Equal("rebind.test", host));
        Assert.Equal(1, createdHandlers);
        Assert.Equal(1, sentRequests);
        Assert.Equal(1, disposedHandlers);
    }

    [Fact]
    public async Task WebPageTransportDoesNotReuseAValidatedSocketForAnotherRequest()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var handler = CopilotWebPageToolSupport.CreateHttpHandler();
        var connectCalls = 0;
        handler.ConnectCallback = async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref connectCalls);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync((IPEndPoint)listener.LocalEndpoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };

        using var client = new HttpClient(handler, disposeHandler: false);
        var releaseFirstConnection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstExchange = ExchangeOnceAndHoldConnectionAsync(
            listener,
            releaseFirstConnection.Task,
            timeout.Token);
        try
        {
            using (var firstRequest = CopilotWebPageToolSupport.CreateWebPageRequestMessage(
                       new Uri("http://pool-policy.test/first")))
            using (var firstResponse = await client.SendAsync(firstRequest, timeout.Token))
            {
                firstResponse.EnsureSuccessStatusCode();
            }

            var secondExchange = ExchangeOnceAsync(listener, timeout.Token);
            using (var secondRequest = CopilotWebPageToolSupport.CreateWebPageRequestMessage(
                       new Uri("http://pool-policy.test/second")))
            using (var secondResponse = await client.SendAsync(secondRequest, timeout.Token))
            {
                secondResponse.EnsureSuccessStatusCode();
            }
            await secondExchange;

            Assert.Equal(2, Volatile.Read(ref connectCalls));
        }
        finally
        {
            releaseFirstConnection.TrySetResult();
            await firstExchange;
        }
    }

    private static async Task ExchangeOnceAndHoldConnectionAsync(
        TcpListener listener,
        Task releaseConnection,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await RespondOnceAsync(client, cancellationToken);
        await releaseConnection.WaitAsync(cancellationToken);
    }

    private static async Task ExchangeOnceAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await RespondOnceAsync(client, cancellationToken);
    }

    private static async Task RespondOnceAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                throw new EndOfStreamException("The HTTP client closed before completing its request headers.");
            if (line.Length == 0)
                break;
        }

        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: keep-alive\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed class SingleResponseHandler(
        Func<HttpResponseMessage> createResponse,
        Action<HttpRequestMessage> inspectRequest,
        Action onDispose) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            inspectRequest(request);
            return Task.FromResult(createResponse());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                onDispose();
            base.Dispose(disposing);
        }
    }
}
