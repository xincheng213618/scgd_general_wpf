using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotChatService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5),
        };

        public async Task<CopilotStreamDelta> CompleteReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            CancellationToken cancellationToken)
        {
            var reasoningBuilder = new StringBuilder();
            var contentBuilder = new StringBuilder();

            await StreamReplyAsync(
                config,
                messages,
                delta =>
                {
                    if (delta.HasReasoning)
                        reasoningBuilder.Append(delta.ReasoningContent);

                    if (delta.HasContent)
                        contentBuilder.Append(delta.Content);
                },
                cancellationToken);

            return new CopilotStreamDelta(reasoningBuilder.ToString(), contentBuilder.ToString());
        }

        public async Task StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(messages);
            ArgumentNullException.ThrowIfNull(onDelta);

            using var request = CreateRequest(config, messages);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(ParseErrorMessage(errorBody, (int)response.StatusCode));
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
            {
                await ReadStreamingResponseAsync(config.ProviderType, response, onDelta, cancellationToken);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var delta = ExtractFinalResponseDelta(config.ProviderType, body);
            if (!delta.HasAny)
                throw new InvalidOperationException("接口返回成功，但没有可显示的文本内容。");

            onDelta(delta);
        }

        private static HttpRequestMessage CreateRequest(CopilotProfileConfig config, IReadOnlyList<CopilotRequestMessage> messages)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(config));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            object payload;
            if (config.ProviderType == CopilotProviderType.AnthropicCompatible)
            {
                request.Headers.Add("x-api-key", config.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                payload = new
                {
                    model = config.Model,
                    system = config.SystemPrompt,
                    max_tokens = config.MaxTokens,
                    temperature = config.Temperature,
                    stream = true,
                    messages = messages.Select(message => new
                    {
                        role = message.Role,
                        content = message.Content,
                    }).ToArray(),
                };
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

                var payloadMessages = new List<object>();
                if (!string.IsNullOrWhiteSpace(config.SystemPrompt))
                {
                    payloadMessages.Add(new
                    {
                        role = "system",
                        content = config.SystemPrompt,
                    });
                }

                payloadMessages.AddRange(messages.Select(message => new
                {
                    role = message.Role,
                    content = message.Content,
                }));

                payload = new
                {
                    model = config.Model,
                    stream = true,
                    temperature = config.Temperature,
                    max_tokens = config.MaxTokens,
                    messages = payloadMessages,
                };
            }

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return request;
        }

        private static Uri BuildEndpoint(CopilotProfileConfig config)
        {
            var baseUrl = (config.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Base URL 不能为空。");

            if (config.ProviderType == CopilotProviderType.AnthropicCompatible)
            {
                if (baseUrl.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
                    return new Uri(baseUrl, UriKind.Absolute);

                if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    return new Uri(baseUrl + "/messages", UriKind.Absolute);

                return new Uri(baseUrl + "/v1/messages", UriKind.Absolute);
            }

            if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return new Uri(baseUrl, UriKind.Absolute);

            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return new Uri(baseUrl + "/chat/completions", UriKind.Absolute);

            return new Uri(baseUrl + "/v1/chat/completions", UriKind.Absolute);
        }

        private static async Task ReadStreamingResponseAsync(
            CopilotProviderType providerType,
            HttpResponseMessage response,
            Action<CopilotStreamDelta> onDelta,
            CancellationToken cancellationToken)
        {
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                if (state is HttpResponseMessage message)
                    message.Dispose();
            }, response);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (true)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (IOException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (line is null)
                    break;

                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var payload = line[5..].Trim();
                if (string.IsNullOrWhiteSpace(payload))
                    continue;

                if (string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
                    break;

                var delta = providerType == CopilotProviderType.AnthropicCompatible
                    ? ExtractAnthropicStreamingDelta(payload)
                    : ExtractOpenAiStreamingDelta(payload);

                if (delta.HasAny)
                    onDelta(delta);
            }
        }

        private static CopilotStreamDelta ExtractOpenAiStreamingDelta(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (!root.TryGetProperty("choices", out var choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0)
                {
                    return CopilotStreamDelta.Empty;
                }

                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta))
                    return ExtractOpenAiDeltaFromElement(delta);

                if (choice.TryGetProperty("message", out var message))
                    return ExtractOpenAiDeltaFromElement(message);
            }
            catch (JsonException)
            {
            }

            return CopilotStreamDelta.Empty;
        }

        private static CopilotStreamDelta ExtractAnthropicStreamingDelta(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                if (root.TryGetProperty("delta", out var delta))
                    return ExtractAnthropicDeltaFromDeltaElement(delta);

                if (root.TryGetProperty("content_block", out var block))
                    return ExtractAnthropicDeltaFromContentBlock(block);

                if (root.TryGetProperty("message", out var message))
                    return ExtractAnthropicDeltaFromMessage(message);
            }
            catch (JsonException)
            {
            }

            return CopilotStreamDelta.Empty;
        }

        private static CopilotStreamDelta ExtractFinalResponseDelta(CopilotProviderType providerType, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return CopilotStreamDelta.Empty;

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                if (providerType == CopilotProviderType.AnthropicCompatible)
                    return ExtractAnthropicDeltaFromMessage(root);

                if (root.TryGetProperty("choices", out var choices)
                    && choices.ValueKind == JsonValueKind.Array
                    && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("message", out var message))
                        return ExtractOpenAiDeltaFromElement(message);

                    if (choice.TryGetProperty("delta", out var delta))
                        return ExtractOpenAiDeltaFromElement(delta);
                }

                return ExtractOpenAiDeltaFromElement(root);
            }
            catch (JsonException)
            {
            }

            return CopilotStreamDelta.Empty;
        }

        private static CopilotStreamDelta ExtractOpenAiDeltaFromElement(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return CopilotStreamDelta.Empty;

            var reasoning = element.TryGetProperty("reasoning_content", out var reasoningElement)
                ? ExtractStringFromElement(reasoningElement)
                : string.Empty;

            var content = element.TryGetProperty("content", out var contentElement)
                ? ExtractStringFromElement(contentElement)
                : element.TryGetProperty("text", out var textElement)
                    ? ExtractStringFromElement(textElement)
                    : string.Empty;

            return new CopilotStreamDelta(reasoning, content);
        }

        private static CopilotStreamDelta ExtractAnthropicDeltaFromMessage(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return CopilotStreamDelta.Empty;

            if (element.TryGetProperty("content", out var content))
                return ExtractAnthropicDeltaFromContentArray(content);

            return ExtractAnthropicDeltaFromContentBlock(element);
        }

        private static CopilotStreamDelta ExtractAnthropicDeltaFromContentArray(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
                return ExtractAnthropicDeltaFromContentBlock(element);

            var reasoningBuilder = new StringBuilder();
            var contentBuilder = new StringBuilder();

            foreach (var item in element.EnumerateArray())
            {
                var delta = ExtractAnthropicDeltaFromContentBlock(item);
                if (delta.HasReasoning)
                    reasoningBuilder.Append(delta.ReasoningContent);
                if (delta.HasContent)
                    contentBuilder.Append(delta.Content);
            }

            return new CopilotStreamDelta(reasoningBuilder.ToString(), contentBuilder.ToString());
        }

        private static CopilotStreamDelta ExtractAnthropicDeltaFromContentBlock(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return new CopilotStreamDelta(string.Empty, element.GetString() ?? string.Empty);

            if (element.ValueKind != JsonValueKind.Object)
                return CopilotStreamDelta.Empty;

            var type = element.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : string.Empty;

            if (string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase))
            {
                var reasoning = element.TryGetProperty("thinking", out var thinkingElement)
                    ? ExtractStringFromElement(thinkingElement)
                    : string.Empty;
                return new CopilotStreamDelta(reasoning, string.Empty);
            }

            if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
            {
                var text = element.TryGetProperty("text", out var textElement)
                    ? ExtractStringFromElement(textElement)
                    : string.Empty;
                return new CopilotStreamDelta(string.Empty, text);
            }

            if (element.TryGetProperty("thinking", out var directThinking))
                return new CopilotStreamDelta(ExtractStringFromElement(directThinking), string.Empty);

            if (element.TryGetProperty("text", out var directText))
                return new CopilotStreamDelta(string.Empty, ExtractStringFromElement(directText));

            return CopilotStreamDelta.Empty;
        }

        private static CopilotStreamDelta ExtractAnthropicDeltaFromDeltaElement(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return CopilotStreamDelta.Empty;

            var type = element.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : string.Empty;

            if (string.Equals(type, "thinking_delta", StringComparison.OrdinalIgnoreCase))
            {
                var thinking = element.TryGetProperty("thinking", out var thinkingElement)
                    ? ExtractStringFromElement(thinkingElement)
                    : string.Empty;
                return new CopilotStreamDelta(thinking, string.Empty);
            }

            if (string.Equals(type, "text_delta", StringComparison.OrdinalIgnoreCase))
            {
                var text = element.TryGetProperty("text", out var textElement)
                    ? ExtractStringFromElement(textElement)
                    : string.Empty;
                return new CopilotStreamDelta(string.Empty, text);
            }

            if (element.TryGetProperty("thinking", out var directThinking))
                return new CopilotStreamDelta(ExtractStringFromElement(directThinking), string.Empty);

            if (element.TryGetProperty("text", out var directText))
                return new CopilotStreamDelta(string.Empty, ExtractStringFromElement(directText));

            if (element.TryGetProperty("output_text", out var outputText))
                return new CopilotStreamDelta(string.Empty, ExtractStringFromElement(outputText));

            return CopilotStreamDelta.Empty;
        }

        private static string ExtractStringFromElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? string.Empty;

            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("reasoning_content", out var reasoningContent))
                    return ExtractStringFromElement(reasoningContent);

                if (element.TryGetProperty("thinking", out var thinking))
                    return ExtractStringFromElement(thinking);

                if (element.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString() ?? string.Empty;

                if (element.TryGetProperty("content", out var content))
                    return ExtractStringFromElement(content);
            }

            if (element.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    builder.Append(item.GetString());
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                if (item.TryGetProperty("thinking", out var thinking))
                    builder.Append(ExtractStringFromElement(thinking));
                else if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    builder.Append(text.GetString());
                else if (item.TryGetProperty("content", out var nestedContent))
                    builder.Append(ExtractStringFromElement(nestedContent));
            }

            return builder.ToString();
        }

        private static string ParseErrorMessage(string errorBody, int statusCode)
        {
            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                try
                {
                    using var document = JsonDocument.Parse(errorBody);
                    var root = document.RootElement;

                    if (root.TryGetProperty("error", out var error))
                    {
                        if (error.ValueKind == JsonValueKind.String)
                            return $"{statusCode}: {error.GetString()}";

                        if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                            return $"{statusCode}: {message.GetString()}";
                    }

                    if (root.TryGetProperty("message", out var topLevelMessage) && topLevelMessage.ValueKind == JsonValueKind.String)
                        return $"{statusCode}: {topLevelMessage.GetString()}";
                }
                catch (JsonException)
                {
                }

                return $"{statusCode}: {errorBody.Trim()}";
            }

            return $"请求失败，HTTP {statusCode}";
        }
    }
}