using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.ClientModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotOpenAiRequestIdTests
{
    [Theory]
    [InlineData(false, "x-request-id")]
    [InlineData(false, "request-id")]
    [InlineData(false, "x-amzn-requestid")]
    [InlineData(true, "x-request-id")]
    [InlineData(true, "request-id")]
    [InlineData(true, "x-amzn-requestid")]
    public async Task OfficialSdkErrorRetainsNormalizedRequestIdThroughExceptionWrappers(bool responsesApi, string header)
    {
        using var handler = new ControlledHandler((_, _) => ErrorResponse(401, " req bad<script> ", header));
        using var httpClient = new HttpClient(handler);
        using var provider = CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(responsesApi), httpClient);

        var error = await GetErrorAsync(provider);

        Assert.Equal(401, error.Status);
        Assert.Equal(1, handler.CallCount);
        Assert.True(error.GetRawResponse()!.Headers.TryGetValue(header, out _));
        Assert.Equal("req_bad_script", CopilotProviderRequestId.Find(error));
        Assert.Equal("req_bad_script", CopilotProviderRequestId.Find(new InvalidOperationException("Outer boundary.", error)));
        handler.AssertRoute(responsesApi);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RequestIdEchoingApiKeyIsRedactedWithoutChangingTheSdkError(bool responsesApi, bool longApiKey)
    {
        var apiKey = longApiKey ? "test-key-" + new string('a', 160) : "test-key";
        var rawRequestId = "req_" + apiKey + "_suffix";
        using var handler = new ControlledHandler((_, _) => ErrorResponse(401, rawRequestId));
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(responsesApi);
        profile.ApiKey = apiKey;
        using var provider = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        profile.ApiKey = "replacement-key-after-client-creation";

        var error = await GetErrorAsync(provider);

        Assert.Equal(401, error.Status);
        Assert.Equal(1, handler.CallCount);
        Assert.True(error.GetRawResponse()!.Headers.TryGetValue("x-request-id", out var rawId));
        Assert.Equal(rawRequestId, rawId);
        Assert.Contains("Controlled HTTP failure.", error.Message, StringComparison.Ordinal);
        var requestId = CopilotProviderRequestId.Find(error);
        Assert.Equal("req_redacted_suffix", requestId);
        Assert.DoesNotContain("test-key", requestId, StringComparison.Ordinal);
        Assert.False(CopilotProviderRetryChatClient.TryClassifyTransientFailure(error, CancellationToken.None, out _, out var statusCode));
        Assert.Equal(401, statusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), CopilotProviderRetryChatClient.ResolveRetryDelay(error, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentSdkErrorsKeepTheirOwnRequestIdAndRetryAfter(bool responsesApi)
    {
        using var handler = new ControlledHandler((_, body) => body.Contains("first-probe", StringComparison.Ordinal)
            ? ErrorResponse(401, "req_test-key_first", retryAfter: "6")
            : ErrorResponse(401, "req_test-key_second", retryAfter: "9"), synchronizeFirstTwo: true);
        using var httpClient = new HttpClient(handler);
        using var provider = CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(responsesApi), httpClient);

        var errors = await Task.WhenAll(GetErrorAsync(provider, "first-probe"), GetErrorAsync(provider, "second-probe"));

        Assert.Equal(2, handler.CallCount);
        Assert.Equal("req_redacted_first", CopilotProviderRequestId.Find(errors[0]));
        Assert.Equal("req_redacted_second", CopilotProviderRequestId.Find(errors[1]));
        Assert.Equal(TimeSpan.FromSeconds(6), CopilotProviderRetryChatClient.ResolveRetryDelay(errors[0], TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromSeconds(9), CopilotProviderRetryChatClient.ResolveRetryDelay(errors[1], TimeSpan.Zero));
        handler.AssertRoute(responsesApi);
    }

    private static Task<ClientResultException> GetErrorAsync(IChatClient provider, string prompt = "Summarize the result.")
        => Assert.ThrowsAnyAsync<ClientResultException>(() => provider.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]));

    private static CopilotProfileConfig CreateProfile(bool responsesApi) => new()
    {
        ProviderType = CopilotProviderType.OpenAICompatible,
        VendorType = responsesApi ? CopilotVendorType.OpenAI : CopilotVendorType.Custom,
        ApiKey = "test-key",
        BaseUrl = responsesApi ? "https://api.openai.com/v1" : "https://example.test/v1",
        Model = responsesApi ? "gpt-5.5" : "test-model",
        MaxTokens = 4_096,
    };

    private static HttpResponseMessage ErrorResponse(int statusCode, string? requestId, string header = "x-request-id", string retryAfter = "7")
    {
        var response = new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                error = new { type = statusCode == 401 ? "authentication_error" : "server_error", message = "Controlled HTTP failure.", code = statusCode.ToString() },
            }), Encoding.UTF8, "application/json"),
        };
        if (requestId != null)
            response.Headers.TryAddWithoutValidation(header, requestId);
        response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        return response;
    }

    private sealed class ControlledHandler(Func<int, string, HttpResponseMessage> response, bool synchronizeFirstTwo = false) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<Uri> _uris = new();
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _uris.Enqueue(request.RequestUri!);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var call = Interlocked.Increment(ref _callCount);
            if (synchronizeFirstTwo && call <= 2)
            {
                if (call == 2)
                    _bothStarted.TrySetResult();
                await _bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            // This is the terminal handler: no request can leave the process.
            return response(call, body);
        }

        public void AssertRoute(bool responsesApi)
        {
            Assert.NotEmpty(_uris);
            Assert.All(_uris, uri => Assert.Equal(responsesApi
                ? new Uri("https://api.openai.com/v1/responses")
                : new Uri("https://example.test/v1/chat/completions"), uri));
        }
    }

    private sealed class CleanupFailureClient(Exception? primary, Exception cleanup) : IChatClient
    {
        public int DisposeCalls { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => new CleanupFailureStream(this, primary, cleanup);
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }

        private sealed class CleanupFailureStream(CleanupFailureClient owner, Exception? primary, Exception cleanup)
            : IAsyncEnumerable<ChatResponseUpdate>, IAsyncEnumerator<ChatResponseUpdate>
        {
            public ChatResponseUpdate Current => throw new InvalidOperationException();
            public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
            public ValueTask<bool> MoveNextAsync() => primary == null ? ValueTask.FromResult(false) : ValueTask.FromException<bool>(primary);
            public ValueTask DisposeAsync()
            {
                owner.DisposeCalls++;
                return ValueTask.FromException(cleanup);
            }
        }
    }

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotOpenAiRequestIdTests-");
        private readonly HttpClient _httpClient;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;
        private readonly CopilotAgentRequest _request;

        public RuntimeFixture(ControlledHandler handler, bool responsesApi)
        {
            _httpClient = new HttpClient(handler, disposeHandler: false);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "openai-request-id-tests", "OpenAI request ID tests", [Tool]);
            _runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([Tool]), new CopilotAgentContextBuilder(), new CopilotToolExecutor(),
                profile => CopilotOpenAiAgentChatClientFactory.Create(profile, _httpClient),
                new EmptyExternalToolProvider(), catalog, new CopilotAgentSkillUsageStore(_directory.FullName));
            _request = new CopilotAgentRequest
            {
                Profile = CreateProfile(responsesApi),
                ConversationId = "openai-request-id-conversation",
                TaskId = "openai-request-id-task",
                WorkspacePath = _directory.FullName,
                UserText = "Run the bounded workspace validation probe and summarize its result.",
                TaskIntentText = "Run the bounded workspace validation probe and summarize its result.",
                Mode = CopilotAgentMode.Code,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Untrusted),
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768, MaxToolCalls = 2, MaxAgentPasses = 1, TotalDuration = TimeSpan.FromSeconds(30),
                },
            };
        }

        public ValidationProbeTool Tool { get; } = new();
        public List<CopilotAgentEvent> Events { get; } = [];
        public Task<CopilotAgentRunResult> RunAsync() => _runtime.RunAsync(_request, Events.Add, CancellationToken.None);
        public void Dispose()
        {
            _httpClient.Dispose();
            _directory.Delete(recursive: true);
        }
    }

    private sealed class ValidationProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "RunWorkspaceValidation";
        public string Description => "Returns deterministic validation evidence.";
        public int CallCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name, Success = true, Summary = "Validation probe completed.", ProcessOperation = "test", ProcessExitCode = 0,
            });
        }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new CopilotExternalToolLease());
    }
}
