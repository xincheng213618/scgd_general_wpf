using Anthropic;
using Anthropic.Exceptions;
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAnthropicHttpErrorBoundaryTests
{
    [Theory]
    [InlineData("6", null, 6)]
    [InlineData("8", "6000", 6)]
    [InlineData("6", "invalid", 6)]
    [InlineData("6", "NaN", 6)]
    [InlineData("6", "-1", 6)]
    [InlineData(null, "999999999", 120)]
    public async Task ErrorBoundaryPreservesBoundedRetryDelayAndRedactedRequestId(string? seconds, string? milliseconds, double expectedSeconds)
    {
        var content = new TrackingContent(JsonSerializer.Serialize(new
        {
            error = new { type = "rate_limit_error", message = "Provider echoed secret-test-key." },
        }));
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = content };
        if (seconds != null)
            response.Headers.TryAddWithoutValidation("Retry-After", seconds);
        if (milliseconds != null)
            response.Headers.TryAddWithoutValidation("Retry-After-Ms", milliseconds);
        response.Headers.TryAddWithoutValidation("request-id", "req_secret-test-key_retry");
        using var boundary = new BoundaryFixture(new ResponseHandler(_ => response));

        var error = await boundary.SendErrorAsync("retry");

        Assert.IsType<AnthropicRateLimitException>(error);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), CopilotProviderRetryChatClient.ResolveRetryDelay(error, TimeSpan.Zero));
        Assert.Equal("req_redacted_retry", CopilotProviderRequestId.Find(error));
        Assert.DoesNotContain("secret-test-key", error.Message, StringComparison.Ordinal);
        Assert.Contains("<redacted>", error.Message, StringComparison.Ordinal);
        Assert.True(content.WasDisposed);
        Assert.Equal(1, content.SerializeCalls);
    }

    [Fact]
    public async Task OversizedUnauthorizedBodyKeepsNonRetryableStatusAndIsNotRead()
    {
        var content = new TrackingContent(new string('x', 300_000) + "secret-test-key");
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = content };
        response.Headers.TryAddWithoutValidation("Retry-After", "8");
        response.Headers.TryAddWithoutValidation("request-id", "req_large_body");
        var transport = new ResponseHandler(_ => response);
        using var boundary = new BoundaryFixture(transport);

        var error = await boundary.SendErrorAsync("large");

        Assert.IsType<AnthropicUnauthorizedException>(error);
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.False(CopilotProviderRetryChatClient.TryClassifyTransientFailure(error, CancellationToken.None, out _, out _));
        Assert.InRange(error.ResponseBody.Length, 1, CopilotUserFacingErrorFormatter.MaximumMessageLength);
        Assert.DoesNotContain("secret-test-key", error.Message, StringComparison.Ordinal);
        Assert.Equal("req_large_body", CopilotProviderRequestId.Find(error));
        Assert.Equal(TimeSpan.FromSeconds(8), CopilotProviderRetryChatClient.ResolveRetryDelay(error, TimeSpan.Zero));
        Assert.Equal(0, content.SerializeCalls);
        Assert.True(content.WasDisposed);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(400, "prompt is too long", true)]
    [InlineData(413, "request rejected", true)]
    [InlineData(401, "prompt is too long", false)]
    public async Task ContextWindowClassificationRespectsHttpStatusAfterSecretRedaction(int statusCode, string message, bool isContextFailure)
    {
        var content = new TrackingContent(JsonSerializer.Serialize(new
        {
            error = new { type = "invalid_request_error", message = message + "; credential secret-test-key" },
        }));
        using var boundary = new BoundaryFixture(new ResponseHandler(_ => new HttpResponseMessage((HttpStatusCode)statusCode) { Content = content }));

        var error = await boundary.SendErrorAsync("context");

        Assert.Equal((HttpStatusCode)statusCode, error.StatusCode);
        Assert.Equal(isContextFailure, CopilotContextWindowFailureClassifier.TryClassify(error, out _));
        Assert.DoesNotContain("secret-test-key", error.Message, StringComparison.Ordinal);
        Assert.True(content.WasDisposed);
    }

    [Fact]
    public async Task ContextWindowMarkerBeyondDisplayLimitRemainsAvailableToClassifier()
    {
        var content = new TrackingContent(JsonSerializer.Serialize(new
        {
            error = new { type = "invalid_request_error", message = new string('x', 6_000) + " prompt is too long; secret-test-key" },
        }));
        using var boundary = new BoundaryFixture(new ResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content }));

        var error = await boundary.SendErrorAsync("long-context");

        Assert.True(CopilotContextWindowFailureClassifier.TryClassify(error, out _));
        Assert.Contains("prompt is too long", error.ResponseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-test-key", error.Message, StringComparison.Ordinal);
        Assert.True(content.WasDisposed);
    }

    [Fact]
    public async Task SuccessfulResponseIsReturnedUnreadAndRemainsOwnedByCaller()
    {
        var content = new TrackingContent("stream content");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        using var boundary = new BoundaryFixture(new ResponseHandler(_ => response));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/success");

        var returned = await boundary.Invoker.SendAsync(request, CancellationToken.None);

        Assert.Same(response, returned);
        Assert.False(content.WasDisposed);
        Assert.Equal(0, content.SerializeCalls);
        returned.Dispose();
        Assert.True(content.WasDisposed);
    }

    [Fact]
    public async Task ConcurrentErrorHeadersStayAttachedToTheirOwnRequest()
    {
        using var boundary = new BoundaryFixture(new GatedResponseHandler());

        var errors = await Task.WhenAll(boundary.SendErrorAsync("first"), boundary.SendErrorAsync("second"));

        Assert.Contains("first", errors[0].ResponseBody, StringComparison.Ordinal);
        Assert.Equal("req_first", CopilotProviderRequestId.Find(errors[0]));
        Assert.Equal(TimeSpan.FromSeconds(6), CopilotProviderRetryChatClient.ResolveRetryDelay(errors[0], TimeSpan.Zero));
        Assert.Contains("second", errors[1].ResponseBody, StringComparison.Ordinal);
        Assert.Equal("req_second", CopilotProviderRequestId.Find(errors[1]));
        Assert.Equal(TimeSpan.FromSeconds(9), CopilotProviderRetryChatClient.ResolveRetryDelay(errors[1], TimeSpan.Zero));
    }

    private sealed class BoundaryFixture : IDisposable
    {
        private readonly IChatClient _provider;

        public BoundaryFixture(HttpMessageHandler inner)
        {
            _provider = CopilotMicrosoftAgentFrameworkRuntime.CreateChatClient(new CopilotProfileConfig
            {
                ProviderType = CopilotProviderType.AnthropicCompatible,
                VendorType = CopilotVendorType.Custom,
                ApiKey = "secret-test-key",
                BaseUrl = "https://example.test",
                Model = "test-model",
                MaxTokens = 4_096,
            });
            var sdk = Assert.IsAssignableFrom<IAnthropicClient>(_provider.GetService(typeof(IAnthropicClient)));
            // Inspect the handler configured by production; replace only its not-yet-attached network transport.
            var handler = Assert.Single(sdk.Handlers);
            Assert.Null(handler.InnerHandler);
            handler.InnerHandler = inner;
            Invoker = new HttpMessageInvoker(handler);
        }

        public HttpMessageInvoker Invoker { get; }

        public async Task<AnthropicApiException> SendErrorAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/" + path);
            return await Assert.ThrowsAnyAsync<AnthropicApiException>(() => Invoker.SendAsync(request, CancellationToken.None));
        }

        public void Dispose()
        {
            Invoker.Dispose();
            _provider.Dispose();
        }
    }

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(response(request));
        }
    }

    private sealed class GatedResponseHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entered;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _entered) == 2)
                _bothEntered.TrySetResult();
            await _bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            var name = request.RequestUri!.AbsolutePath.Trim('/');
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new TrackingContent(JsonSerializer.Serialize(new { error = new { type = "rate_limit_error", message = name } })),
            };
            response.Headers.TryAddWithoutValidation("request-id", "req_" + name);
            response.Headers.TryAddWithoutValidation("Retry-After", name == "first" ? "6" : "9");
            return response;
        }
    }

    private sealed class TrackingContent(string text) : HttpContent
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(text);
        public bool WasDisposed { get; private set; }
        public int SerializeCalls { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            SerializeCalls++;
            await stream.WriteAsync(_bytes);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
