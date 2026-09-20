#pragma warning disable OPENAI001, SCME0001
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotOpenAiPromptCacheDiagnosticsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompletedResponsesCompareWithoutChangingHistoryOptionsOrUsage(bool streaming)
    {
        using var handler = new CapturingHandler((call, _) => Response(call, streaming,
            new { type = "cache_miss", reason = "tools_changed", comparison_reusable_tokens = 9999, cache_missed_tokens = 8000 }));
        using var httpClient = new HttpClient(handler);
        var diagnostics = new List<string>();
        using var client = CreateClient(httpClient, diagnostics.Add);
        var rawOptions = new CreateResponseOptions { TextOptions = new ResponseTextOptions() };
        rawOptions.Patch.Set("$.safety_identifier"u8, "safe-user-identifier");
        rawOptions.TextOptions.Patch.Set("$.verbosity"u8, "low");
        var options = new ChatOptions
        {
            Instructions = "Preserve these instructions.",
            MaxOutputTokens = 512,
            RawRepresentationFactory = _ => rawOptions,
        };

        for (var call = 1; call <= 3; call++)
        {
            var result = await SendAsync(client, streaming, options);
            Assert.Equal("Answer.", result.Text);
            Assert.Equal(10, result.Usage!.InputTokenCount);
            Assert.Equal(5, result.Usage.OutputTokenCount);
            Assert.Equal(15, result.Usage.TotalTokenCount);
            Assert.Equal(4, result.Usage.CachedInputTokenCount);
        }

        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Contains("tools_changed", diagnostic, StringComparison.Ordinal);
            Assert.Contains("8000", diagnostic, StringComparison.Ordinal);
            Assert.Contains("实际缓存用量以供应商 usage 为准", diagnostic, StringComparison.Ordinal);
            Assert.DoesNotContain("resp_", diagnostic, StringComparison.Ordinal);
        });
        Assert.False(rawOptions.Patch.Contains("$.prompt_cache_options"u8));
        for (var index = 0; index < handler.Payloads.Count; index++)
        {
            using var payload = JsonDocument.Parse(handler.Payloads[index]);
            var root = payload.RootElement;
            Assert.False(root.GetProperty("store").GetBoolean());
            Assert.False(root.TryGetProperty("previous_response_id", out _));
            Assert.Equal("safe-user-identifier", root.GetProperty("safety_identifier").GetString());
            Assert.Equal("low", root.GetProperty("text").GetProperty("verbosity").GetString());
            Assert.Equal("Preserve these instructions.", root.GetProperty("instructions").GetString());
            Assert.Equal(512, root.GetProperty("max_output_tokens").GetInt32());
            Assert.Contains("Read the result.", root.GetProperty("input").GetRawText(), StringComparison.Ordinal);
            if (index == 0)
                Assert.False(root.TryGetProperty("prompt_cache_options", out _));
            else
                Assert.Equal("resp_" + index, ComparisonId(root));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IncompleteResponseCannotReplaceCompletedComparisonBaseline(bool streaming)
    {
        using var handler = new CapturingHandler((call, _) => Response(call, streaming, null,
            status: call == 2 ? "incomplete" : "completed"));
        using var httpClient = new HttpClient(handler);
        var diagnostics = new List<string>();
        using var client = CreateClient(httpClient, diagnostics.Add);

        await SendAsync(client, streaming);
        await SendAsync(client, streaming);
        await SendAsync(client, streaming);

        Assert.Single(diagnostics);
        using var payload = JsonDocument.Parse(handler.Payloads[2]);
        Assert.Equal("resp_1", ComparisonId(payload.RootElement));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedRequestKeepsOriginalFailureAndPreviousBaseline(bool streaming)
    {
        using var handler = new CapturingHandler((call, _) => call == 2
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"error\":{\"message\":\"Controlled failure.\",\"type\":\"authentication_error\"}}") }
            : Response(call, streaming, null));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, _ => { });

        await SendAsync(client, streaming);
        var error = await Assert.ThrowsAnyAsync<System.ClientModel.ClientResultException>(() => SendAsync(client, streaming));
        Assert.Equal(401, error.Status);
        await SendAsync(client, streaming);

        Assert.Equal(3, handler.Payloads.Count);
        using var payload = JsonDocument.Parse(handler.Payloads[2]);
        Assert.Equal("resp_1", ComparisonId(payload.RootElement));
    }

    [Theory]
    [InlineData("cache_hit", "cache_hit")]
    [InlineData("comparison_response_not_found", "comparison_response_not_found")]
    [InlineData("unavailable", "unavailable")]
    [InlineData("future_status_private_text", "unavailable")]
    public async Task DiagnosticOutcomesRemainNonAuthoritativeAndUseFreshBaselines(string type, string expected)
    {
        using var handler = new CapturingHandler((call, _) => Response(call, true, new { type }));
        using var httpClient = new HttpClient(handler);
        var diagnostics = new List<string>();
        using var client = CreateClient(httpClient, diagnostics.Add);

        for (var call = 0; call < 3; call++)
            Assert.Equal("Answer.", (await SendAsync(client, true)).Text);

        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Contains(expected, diagnostic, StringComparison.Ordinal);
            Assert.DoesNotContain("private_text", diagnostic, StringComparison.Ordinal);
        });
        using var payload = JsonDocument.Parse(handler.Payloads[2]);
        Assert.Equal("resp_2", ComparisonId(payload.RootElement));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"type\":7}")]
    [InlineData("{\"type\":\"cache_miss\",\"reason\":\"private-provider-text\",\"comparison_reusable_tokens\":-1,\"cache_missed_tokens\":999999999999999999999}")]
    [InlineData("{\"type\":\"cache_miss\",\"reason\":\"input_changed\",\"comparison_reusable_tokens\":\"not_a_number\",\"cache_missed_tokens\":true}")]
    public async Task MalformedOptionalFieldsNeverExposePayloadOrInvalidateAnswer(string json)
    {
        using var diagnosticDocument = JsonDocument.Parse(json);
        using var handler = new CapturingHandler((call, _) => Response(call, true, diagnosticDocument.RootElement));
        using var httpClient = new HttpClient(handler);
        var diagnostics = new List<string>();
        using var client = CreateClient(httpClient, diagnostics.Add);
        await SendAsync(client, true);

        Assert.Equal("Answer.", (await SendAsync(client, true)).Text);

        var diagnostic = Assert.Single(diagnostics);
        Assert.DoesNotContain("private-provider-text", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("Token：", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiagnosticObserverFailureCannotFailCompletedResponse()
    {
        using var handler = new CapturingHandler((call, _) => Response(call, true, new { type = "cache_hit" }));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, _ => throw new InvalidOperationException("Observer failure."));
        await SendAsync(client, true);
        Assert.Equal("Answer.", (await SendAsync(client, true)).Text);
        Assert.Equal(2, handler.Payloads.Count);
    }

    [Fact]
    public async Task NewAgentClientDoesNotReusePreviousRunsResponseId()
    {
        using var handler = new CapturingHandler((call, _) => Response(call, true, null));
        using var httpClient = new HttpClient(handler);
        using (var first = CreateClient(httpClient, _ => { }))
            await SendAsync(first, true);
        using (var second = CreateClient(httpClient, _ => { }))
            await SendAsync(second, true);

        Assert.All(handler.Payloads, json =>
        {
            using var payload = JsonDocument.Parse(json);
            Assert.False(payload.RootElement.TryGetProperty("prompt_cache_options", out _));
        });
    }

    [Theory]
    [InlineData("gpt-5.5", false)]
    [InlineData("gpt-5.6-sol", true)]
    [InlineData("gpt-5.6-luna", true)]
    [InlineData("gpt-5.10", true)]
    [InlineData("gpt-6-astra", true)]
    [InlineData("gpt-6-astra-2026-09-03", true)]
    [InlineData("gpt-4o", false)]
    [InlineData("custom-model", false)]
    public void DiagnosticsOnlyUseSupportedOfficialResponsesModels(string model, bool expected)
    {
        var profile = CreateProfile();
        profile.Model = model;
        Assert.Equal(expected, CopilotOpenAiRequestPolicy.CanRequestPromptCacheDiagnostics(profile));
        profile.BaseUrl = "https://example.test/v1";
        Assert.False(CopilotOpenAiRequestPolicy.CanRequestPromptCacheDiagnostics(profile));
        profile.BaseUrl = "https://api.openai.com/v1";
        profile.VendorType = CopilotVendorType.Custom;
        Assert.False(CopilotOpenAiRequestPolicy.CanRequestPromptCacheDiagnostics(profile));
    }

    private static CopilotProfileConfig CreateProfile() => new()
    {
        VendorType = CopilotVendorType.OpenAI,
        ProviderType = CopilotProviderType.OpenAICompatible,
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = "test-key",
        Model = "gpt-6-astra",
    };

    private static IChatClient CreateClient(HttpClient httpClient, Action<string> onDiagnostic)
        => new CopilotOpenAiPromptCacheChatClient(CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(), httpClient), onDiagnostic);

    private static Task<ChatResponse> SendAsync(IChatClient client, bool streaming, ChatOptions? options = null)
        => streaming
            ? client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Read the result.")], options).ToChatResponseAsync()
            : client.GetResponseAsync([new ChatMessage(ChatRole.User, "Read the result.")], options);

    private static string? ComparisonId(JsonElement root)
        => root.GetProperty("prompt_cache_options").GetProperty("comparison_response_id").GetString();

    private static HttpResponseMessage Response(int call, bool streaming, object? diagnostics, string status = "completed")
    {
        var response = new
        {
            id = "resp_" + call, @object = "response", created_at = 1234567890, model = "gpt-6-astra", status,
            incomplete_details = status == "incomplete" ? new { reason = "max_output_tokens" } : null,
            output = new[] { new { type = "message", id = "msg_" + call, role = "assistant", status = "completed", content = new[] { new { type = "output_text", text = "Answer.", annotations = Array.Empty<object>() } } } },
            usage = new { input_tokens = 10, output_tokens = 5, total_tokens = 15, input_tokens_details = new { cached_tokens = 4 }, output_tokens_details = new { reasoning_tokens = 0 } },
            prompt_cache_diagnostics = diagnostics,
        };
        var body = streaming
            ? "data: " + JsonSerializer.Serialize(new { type = "response.created", sequence_number = 0, response = new { response.id, @object = "response", response.created_at, response.model, status = "in_progress", output = Array.Empty<object>() } }) + "\n\n"
                + "data: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", sequence_number = 1, item_id = "msg_" + call, output_index = 0, content_index = 0, delta = "Answer." }) + "\n\n"
                + "data: " + JsonSerializer.Serialize(new { type = "response." + status, sequence_number = 2, response }) + "\n\n"
            : JsonSerializer.Serialize(response);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, streaming ? "text/event-stream" : "application/json"),
        };
    }

    private sealed class CapturingHandler(Func<int, string, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("api.openai.com", request.RequestUri!.Host);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Payloads.Add(body);
            return responseFactory(Payloads.Count, body);
        }
    }
}
