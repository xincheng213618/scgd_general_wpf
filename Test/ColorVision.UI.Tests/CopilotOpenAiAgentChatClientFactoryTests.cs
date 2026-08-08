using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotOpenAiAgentChatClientFactoryTests
{
    private const string TextResponseStream =
        """
        data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_test","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"in_progress","output":[]}}

        data: {"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_test","output_index":0,"content_index":0,"delta":"Responses adapter "}

        data: {"type":"response.output_text.delta","sequence_number":2,"item_id":"msg_test","output_index":0,"content_index":0,"delta":"OK."}

        data: {"type":"response.completed","sequence_number":3,"response":{"id":"resp_test","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"message","id":"msg_test","role":"assistant","status":"completed","content":[{"type":"output_text","text":"Responses adapter OK.","annotations":[]}]}],"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15,"input_tokens_details":{"cached_tokens":0},"output_tokens_details":{"reasoning_tokens":0}}}}

        data: [DONE]

        """;

    private const string FunctionCallResponseStream =
        """
        data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_tool","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"in_progress","output":[]}}

        data: {"type":"response.output_item.added","sequence_number":1,"output_index":0,"item":{"type":"function_call","id":"fc_test","call_id":"call_test","name":"read_file","arguments":"","status":"in_progress"}}

        data: {"type":"response.function_call_arguments.delta","sequence_number":2,"item_id":"fc_test","output_index":0,"delta":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}"}

        data: {"type":"response.function_call_arguments.done","sequence_number":3,"item_id":"fc_test","output_index":0,"name":"read_file","arguments":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}"}

        data: {"type":"response.output_item.done","sequence_number":4,"output_index":0,"item":{"type":"function_call","id":"fc_test","call_id":"call_test","name":"read_file","arguments":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}","status":"completed"}}

        data: {"type":"response.completed","sequence_number":5,"response":{"id":"resp_tool","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"function_call","id":"fc_test","call_id":"call_test","name":"read_file","arguments":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}","status":"completed"}],"usage":{"input_tokens":12,"output_tokens":8,"total_tokens":20,"input_tokens_details":{"cached_tokens":0},"output_tokens_details":{"reasoning_tokens":0}}}}

        data: [DONE]

        """;

    [Fact]
    public async Task OfficialOpenAiAgentUsesStatelessResponsesStreamingContract()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(
                CopilotVendorType.OpenAI,
                "https://api.openai.com/v1/responses",
                "gpt-5.5"),
            httpClient);
        var options = CreateToolOptions();

        var response = await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Inspect the file.")],
                options)
            .ToChatResponseAsync();

        Assert.Equal(
            new Uri("https://api.openai.com/v1/responses"),
            handler.LastRequestUri);
        Assert.Equal("Responses adapter OK.", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(10, response.Usage?.InputTokenCount);
        Assert.Equal(5, response.Usage?.OutputTokenCount);
        Assert.Equal(15, response.Usage?.TotalTokenCount);

        using var payload = JsonDocument.Parse(handler.LastPayload);
        var root = payload.RootElement;
        Assert.Equal("gpt-5.5", root.GetProperty("model").GetString());
        Assert.Equal(512, root.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal(
            "Use the supplied tool when evidence is required.",
            root.GetProperty("instructions").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Contains(
            root.GetProperty("include").EnumerateArray(),
            item => item.GetString() == "reasoning.encrypted_content");
        Assert.Contains(
            root.GetProperty("tools").EnumerateArray(),
            tool => tool.GetProperty("name").GetString() == "read_file");
    }

    [Fact]
    public async Task OfficialOpenAiAgentMapsResponsesFunctionCallsForHarnessExecution()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            FunctionCallResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(
                CopilotVendorType.OpenAI,
                "https://api.openai.com/v1",
                "gpt-5.5"),
            httpClient);

        var response = await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Inspect the evidence.")],
                CreateToolOptions())
            .ToChatResponseAsync();
        var functionCall = Assert.Single(
            response.Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>());

        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Equal("call_test", functionCall.CallId);
        Assert.Equal("read_file", functionCall.Name);
        Assert.Equal(
            @"C:\workspace\evidence.txt",
            functionCall.Arguments?["path"]?.ToString());
    }

    [Theory]
    [InlineData("fast", "high", "priority", "high")]
    [InlineData("flex", "medium", "flex", "medium")]
    [InlineData("scale", "low", "scale", "low")]
    public async Task OfficialOpenAiAgentPreservesCodexResponsePreferencesOnTheWire(
        string configuredServiceTier,
        string configuredVerbosity,
        string expectedServiceTier,
        string expectedVerbosity)
    {
        Assert.True(CopilotCodexModelVerbositySelection.TryParse(
            configuredVerbosity,
            out var verbosity));
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1",
            "gpt-5.5");
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest
        {
            Profile = profile,
            CodexReasoningEffort = CopilotCodexReasoningEffort.Minimal,
            CodexReasoningSummary = CopilotCodexReasoningSummary.Concise,
            CodexServiceTier = configuredServiceTier,
            CodexModelVerbosity = verbosity,
        };

        await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Use the configured response preferences.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, []))
            .ToChatResponseAsync();

        using var payload = JsonDocument.Parse(handler.LastPayload);
        var root = payload.RootElement;
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal(
            expectedServiceTier,
            root.GetProperty("service_tier").GetString());
        Assert.Equal(
            expectedVerbosity,
            root.GetProperty("text").GetProperty("verbosity").GetString());
        Assert.Equal("minimal", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("concise", root.GetProperty("reasoning").GetProperty("summary").GetString());
    }

    [Theory]
    [InlineData("minimal", "concise", "minimal", "concise")]
    [InlineData("xhigh", "detailed", "xhigh", "detailed")]
    [InlineData("high", "none", "high", null)]
    [InlineData(null, "auto", null, "auto")]
    [InlineData(null, "none", null, null)]
    public async Task OfficialOpenAiAgentPreservesCodexReasoningOptionsOnTheResponsesWire(
        string? configuredEffort,
        string? configuredSummary,
        string? expectedEffort,
        string? expectedSummary)
    {
        var effort = CopilotCodexReasoningEffort.Unspecified;
        var summary = CopilotCodexReasoningSummary.Unspecified;
        if (configuredEffort != null)
            Assert.True(CopilotCodexReasoningEffortSelection.TryParse(configuredEffort, out effort));
        if (configuredSummary != null)
            Assert.True(CopilotCodexReasoningSummarySelection.TryParse(configuredSummary, out summary));
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1",
            "gpt-5.5");
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest
        {
            Profile = profile,
            CodexReasoningEffort = effort,
            CodexReasoningSummary = summary,
        };

        await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Use the configured reasoning contract.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, []))
            .ToChatResponseAsync();

        using var payload = JsonDocument.Parse(handler.LastPayload);
        if (expectedEffort == null
            && expectedSummary == null
            && string.Equals(configuredSummary, "none", StringComparison.Ordinal))
        {
            Assert.False(payload.RootElement.TryGetProperty("reasoning", out _));
            return;
        }

        var reasoning = payload.RootElement.GetProperty("reasoning");
        if (expectedEffort == null)
            Assert.False(reasoning.TryGetProperty("effort", out _));
        else
            Assert.Equal(expectedEffort, reasoning.GetProperty("effort").GetString());
        if (expectedSummary == null)
            Assert.False(reasoning.TryGetProperty("summary", out _));
        else
            Assert.Equal(expectedSummary, reasoning.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task ThirdPartyCompatibleAgentKeepsChatCompletionsTransport()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.BadRequest,
            """{"error":{"message":"capture complete","type":"invalid_request_error"}}""",
            "application/json");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.Custom,
            "https://example.test/v1",
            "gpt-5.5-compatible");

        Assert.True(profile.EnsureValid());
        Assert.Equal(CopilotVendorType.OpenAI, profile.VendorType);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            profile,
            httpClient);

        await Assert.ThrowsAnyAsync<Exception>(
            () => client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Keep proxy compatibility.")]));

        Assert.Equal(
            new Uri("https://example.test/v1/chat/completions"),
            handler.LastRequestUri);
        using var payload = JsonDocument.Parse(handler.LastPayload);
        Assert.False(payload.RootElement.TryGetProperty("store", out _));
    }

    private static ChatOptions CreateToolOptions()
    {
        return new ChatOptions
        {
            Instructions = "Use the supplied tool when evidence is required.",
            MaxOutputTokens = 512,
            Tools =
            [
                AIFunctionFactory.Create(
                    (string path) => path,
                    "read_file",
                    "Reads a local file."),
            ],
        };
    }

    private static CopilotProfileConfig CreateProfile(
        CopilotVendorType vendorType,
        string baseUrl,
        string model)
    {
        return new CopilotProfileConfig
        {
            VendorType = vendorType,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = baseUrl,
            Model = model,
            MaxTokens = 4_096,
        };
    }

    private sealed class CapturingHandler(
        HttpStatusCode statusCode,
        string response,
        string mediaType) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public string LastPayload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastPayload = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    response,
                    Encoding.UTF8,
                    mediaType),
            };
        }
    }
}
