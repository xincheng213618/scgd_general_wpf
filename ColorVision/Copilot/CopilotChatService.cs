#pragma warning disable CA1822,CA1861
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        private const int MaximumProviderErrorResponseBytes = 256 * 1024;
        private const int MaximumNonStreamingResponseBytes = 4 * 1024 * 1024;
        private const int MaximumStreamingResponseBytes = 8 * 1024 * 1024;
        private const int MaximumStreamingLineCharacters = 1024 * 1024;
        private const string ProviderStatusCodeDataKey = "ColorVision.Copilot.ProviderStatusCode";
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();
        private readonly HttpClient _httpClient;
        private readonly int _maximumAttempts;
        private readonly Func<int, TimeSpan> _retryDelayFactory;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

        public CopilotChatService()
            : this(SharedHttpClient)
        {
        }

        public CopilotChatService(HttpClient httpClient)
            : this(
                httpClient,
                CopilotProviderRetryChatClient.DefaultMaximumAttempts,
                CopilotProviderRetryChatClient.CreateDefaultDelay,
                Task.Delay)
        {
        }

        internal CopilotChatService(
            HttpClient httpClient,
            int maximumAttempts,
            Func<int, TimeSpan> retryDelayFactory,
            Func<TimeSpan, CancellationToken, Task> delayAsync)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
            _maximumAttempts = maximumAttempts;
            _retryDelayFactory = retryDelayFactory ?? throw new ArgumentNullException(nameof(retryDelayFactory));
            _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        }

        public Task<CopilotChatReply> CompleteReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            CancellationToken cancellationToken) =>
            CompleteReplyAsync(config, messages, imageAttachments: null, cancellationToken);

        internal async Task<CopilotChatReply> CompleteReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            IReadOnlyList<CopilotAttachmentItem>? imageAttachments,
            CancellationToken cancellationToken)
        {
            var reasoningBuilder = new StringBuilder();
            var contentBuilder = new StringBuilder();

            var usage = await StreamReplyAsync(
                config,
                messages,
                delta =>
                {
                    if (delta.HasReasoning)
                        reasoningBuilder.Append(delta.ReasoningContent);

                    if (delta.HasContent)
                        contentBuilder.Append(delta.Content);
                },
                onRetry: null,
                imageAttachments: imageAttachments,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return new CopilotChatReply(
                new CopilotStreamDelta(reasoningBuilder.ToString(), contentBuilder.ToString()),
                usage);
        }

        public Task<CopilotTokenUsage> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            CancellationToken cancellationToken) =>
            StreamReplyAsync(config, messages, onDelta, onRetry: null, imageAttachments: null, cancellationToken);

        internal async Task<CopilotTokenUsage> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotProviderRetryInfo>? onRetry,
            CancellationToken cancellationToken) =>
            await StreamReplyAsync(config, messages, onDelta, onRetry, imageAttachments: null, cancellationToken).ConfigureAwait(false);

        internal async Task<CopilotTokenUsage> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotProviderRetryInfo>? onRetry,
            IReadOnlyList<CopilotAttachmentItem>? imageAttachments,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(messages);
            ArgumentNullException.ThrowIfNull(onDelta);
            var imagePayloads = await CopilotImagePayloadLoader.LoadAsync(imageAttachments, cancellationToken).ConfigureAwait(false);

            for (var attempt = 1; ; attempt++)
            {
                var responseStarted = false;
                try
                {
                    return await StreamReplyAttemptAsync(
                        config,
                        messages,
                        imagePayloads,
                        delta =>
                        {
                            responseStarted = true;
                            onDelta(delta);
                        },
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (TryCreateRetry(exception, attempt, responseStarted, cancellationToken, out var retry))
                {
                    onRetry?.Invoke(retry);
                    await _delayAsync(retry.Delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task<CopilotTokenUsage> StreamReplyAttemptAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            IReadOnlyList<CopilotImagePayload> imagePayloads,
            Action<CopilotStreamDelta> onDelta,
            CancellationToken cancellationToken)
        {
            using var request = CreateRequest(config, messages, imagePayloads);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody;
                try
                {
                    errorBody = await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                        response.Content,
                        MaximumProviderErrorResponseBytes,
                        "Provider error response",
                        cancellationToken).ConfigureAwait(false);
                }
                catch (CopilotHttpContentSizeLimitException exception)
                {
                    var oversizedResponseException = new InvalidOperationException($"{(int)response.StatusCode}: {exception.Message}", exception);
                    oversizedResponseException.Data[ProviderStatusCodeDataKey] = (int)response.StatusCode;
                    throw oversizedResponseException;
                }

                var providerException = new InvalidOperationException(ParseErrorMessage(errorBody, (int)response.StatusCode, config.ApiKey));
                providerException.Data[ProviderStatusCodeDataKey] = (int)response.StatusCode;
                throw providerException;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
                return await ReadStreamingResponseAsync(config, response, onDelta, cancellationToken).ConfigureAwait(false);

            var body = await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                response.Content,
                MaximumNonStreamingResponseBytes,
                "Non-streaming provider response",
                cancellationToken).ConfigureAwait(false);
            var reply = ExtractFinalResponseReply(config.ProviderType, body);
            if (!reply.Delta.HasAny)
                throw new InvalidOperationException("The API returned successfully, but no displayable text was found.");

            onDelta(reply.Delta);
            return reply.Usage;
        }

        private bool TryCreateRetry(
            Exception exception,
            int failedAttempt,
            bool responseStarted,
            CancellationToken cancellationToken,
            out CopilotProviderRetryInfo retry)
        {
            retry = null!;
            if (responseStarted || failedAttempt >= _maximumAttempts || cancellationToken.IsCancellationRequested)
                return false;

            string failureKind;
            int? statusCode;
            if (TryGetProviderStatusCode(exception, out var providerStatusCode))
            {
                statusCode = providerStatusCode;
                failureKind = "HTTP " + statusCode.Value;
                if (!CopilotProviderRetryChatClient.IsTransientStatusCode(statusCode.Value))
                    return false;
            }
            else if (!CopilotProviderRetryChatClient.TryClassifyTransientFailure(
                exception,
                cancellationToken,
                out failureKind,
                out statusCode))
            {
                return false;
            }

            retry = new CopilotProviderRetryInfo(
                failedAttempt,
                failedAttempt + 1,
                _maximumAttempts,
                _retryDelayFactory(failedAttempt),
                failureKind,
                statusCode);
            return true;
        }

        private static bool TryGetProviderStatusCode(Exception exception, out int statusCode)
        {
            if (exception.Data[ProviderStatusCodeDataKey] is int value && value > 0)
            {
                statusCode = value;
                return true;
            }

            statusCode = 0;
            return false;
        }

        private static HttpRequestMessage CreateRequest(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            IReadOnlyList<CopilotImagePayload> imagePayloads)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(config));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            Dictionary<string, object?> payload;
            var lastUserMessageIndex = FindLastUserMessageIndex(messages);
            if (imagePayloads.Count > 0 && lastUserMessageIndex < 0)
                throw new InvalidOperationException("Image input requires a user message.");
            if (config.ProviderType == CopilotProviderType.AnthropicCompatible)
            {
                request.Headers.Add("x-api-key", config.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                var systemPrompt = config.EffectiveSystemPrompt;
                payload = new Dictionary<string, object?>
                {
                    ["model"] = config.Model,
                    ["system"] = systemPrompt,
                    ["max_tokens"] = config.MaxTokens,
                    ["stream"] = true,
                    ["messages"] = messages.Select((message, index) => new Dictionary<string, object?>
                    {
                        ["role"] = message.Role,
                        ["content"] = index == lastUserMessageIndex
                            ? BuildAnthropicMessageContent(message.Content, imagePayloads)
                            : message.Content,
                    }).ToArray(),
                };

                if (CopilotReasoningRequestMapper.ShouldIncludeTemperature(config))
                    payload["temperature"] = config.Temperature;
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

                var systemPrompt = config.EffectiveSystemPrompt;
                var payloadMessages = new List<object>();
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                {
                    payloadMessages.Add(new
                    {
                        role = "system",
                        content = systemPrompt,
                    });
                }

                payloadMessages.AddRange(messages.Select((message, index) => new Dictionary<string, object?>
                {
                    ["role"] = message.Role,
                    ["content"] = index == lastUserMessageIndex
                        ? BuildOpenAiMessageContent(message.Content, imagePayloads)
                        : message.Content,
                }));

                payload = new Dictionary<string, object?>
                {
                    ["model"] = config.Model,
                    ["stream"] = true,
                    ["max_tokens"] = config.MaxTokens,
                    ["stream_options"] = new
                    {
                        include_usage = true,
                    },
                    ["messages"] = payloadMessages,
                };

                if (CopilotReasoningRequestMapper.ShouldIncludeTemperature(config))
                    payload["temperature"] = config.Temperature;
            }

            CopilotReasoningRequestMapper.Apply(config, payload);

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return request;
        }

        private static int FindLastUserMessageIndex(IReadOnlyList<CopilotRequestMessage> messages)
        {
            for (var index = messages.Count - 1; index >= 0; index--)
            {
                if (string.Equals(messages[index].Role, "user", StringComparison.OrdinalIgnoreCase))
                    return index;
            }

            return -1;
        }

        private static object BuildAnthropicMessageContent(string? text, IReadOnlyList<CopilotImagePayload> images)
        {
            if (images.Count == 0)
                return text ?? string.Empty;

            var content = new List<object>();
            if (!string.IsNullOrWhiteSpace(text))
                content.Add(new { type = "text", text });
            foreach (var image in images)
            {
                content.Add(new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = image.MediaType,
                        data = image.Base64Data,
                    },
                });
            }
            return content;
        }

        private static object BuildOpenAiMessageContent(string? text, IReadOnlyList<CopilotImagePayload> images)
        {
            if (images.Count == 0)
                return text ?? string.Empty;

            var content = new List<object>();
            if (!string.IsNullOrWhiteSpace(text))
                content.Add(new { type = "text", text });
            foreach (var image in images)
            {
                content.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = $"data:{image.MediaType};base64,{image.Base64Data}",
                        detail = "auto",
                    },
                });
            }
            return content;
        }

        private static Uri BuildEndpoint(CopilotProfileConfig config)
        {
            var baseUrl = (config.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Base URL is required.");

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

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            })
            {
                Timeout = TimeSpan.FromMinutes(5),
            };
        }

        private static async Task<CopilotTokenUsage> ReadStreamingResponseAsync(
            CopilotProfileConfig config,
            HttpResponseMessage response,
            Action<CopilotStreamDelta> onDelta,
            CancellationToken cancellationToken)
        {
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                if (state is HttpResponseMessage message)
                    message.Dispose();
            }, response);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new CopilotBoundedTextLineReader(
                stream,
                Encoding.UTF8,
                MaximumStreamingResponseBytes,
                MaximumStreamingLineCharacters,
                "Provider event stream");
            var usage = CopilotTokenUsage.Empty;
            var receivedDisplayableText = false;

            while (true)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
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

                if (TryParseStreamingError(payload, config.ApiKey, out var streamingError))
                    throw new InvalidOperationException(streamingError);

                var reply = config.ProviderType == CopilotProviderType.AnthropicCompatible
                    ? ExtractAnthropicStreamingReply(payload)
                    : ExtractOpenAiStreamingReply(payload);

                if (reply.Usage.HasAny)
                    usage = usage.MergeProgress(reply.Usage);

                if (reply.Delta.HasAny)
                {
                    receivedDisplayableText = true;
                    onDelta(reply.Delta);
                }
            }

            if (!receivedDisplayableText)
                throw new InvalidOperationException("The API stream completed successfully, but no displayable text was found.");

            return usage;
        }

        private static bool TryParseStreamingError(string payload, string? apiKey, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (!TryExtractProviderErrorDetail(root, out var detail))
                    return false;

                errorMessage = CopilotUserFacingErrorFormatter.Sanitize($"Provider stream error: {detail}", apiKey);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static CopilotChatReply ExtractOpenAiStreamingReply(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                var usage = ExtractOpenAiUsage(root);
                if (!root.TryGetProperty("choices", out var choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0)
                {
                    return new CopilotChatReply(CopilotStreamDelta.Empty, usage);
                }

                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta))
                    return new CopilotChatReply(ExtractOpenAiDeltaFromElement(delta), usage);

                if (choice.TryGetProperty("message", out var message))
                    return new CopilotChatReply(ExtractOpenAiDeltaFromElement(message), usage);
            }
            catch (JsonException)
            {
            }

            return CopilotChatReply.Empty;
        }

        private static CopilotChatReply ExtractAnthropicStreamingReply(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                var usage = ExtractAnthropicUsage(root);

                if (root.TryGetProperty("delta", out var delta))
                    return new CopilotChatReply(ExtractAnthropicDeltaFromDeltaElement(delta), usage);

                if (root.TryGetProperty("content_block", out var block))
                    return new CopilotChatReply(ExtractAnthropicDeltaFromContentBlock(block), usage);

                if (root.TryGetProperty("message", out var message))
                    return new CopilotChatReply(ExtractAnthropicDeltaFromMessage(message), usage);
            }
            catch (JsonException)
            {
            }

            return CopilotChatReply.Empty;
        }

        private static CopilotChatReply ExtractFinalResponseReply(CopilotProviderType providerType, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return CopilotChatReply.Empty;

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                if (providerType == CopilotProviderType.AnthropicCompatible)
                    return new CopilotChatReply(ExtractAnthropicDeltaFromMessage(root), ExtractAnthropicUsage(root));

                if (root.TryGetProperty("choices", out var choices)
                    && choices.ValueKind == JsonValueKind.Array
                    && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    var usage = ExtractOpenAiUsage(root);
                    if (choice.TryGetProperty("message", out var message))
                        return new CopilotChatReply(ExtractOpenAiDeltaFromElement(message), usage);

                    if (choice.TryGetProperty("delta", out var delta))
                        return new CopilotChatReply(ExtractOpenAiDeltaFromElement(delta), usage);
                }

                return new CopilotChatReply(ExtractOpenAiDeltaFromElement(root), ExtractOpenAiUsage(root));
            }
            catch (JsonException)
            {
            }

            return CopilotChatReply.Empty;
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

        private static CopilotTokenUsage ExtractOpenAiUsage(JsonElement element)
        {
            if (!TryGetUsageElement(element, out var usageElement))
                return CopilotTokenUsage.Empty;

            return ExtractUsage(
                usageElement,
                new[] { "prompt_tokens", "input_tokens" },
                new[] { "completion_tokens", "output_tokens" });
        }

        private static CopilotTokenUsage ExtractAnthropicUsage(JsonElement element)
        {
            if (TryGetUsageElement(element, out var usageElement))
                return ExtractUsage(
                    usageElement,
                    new[] { "input_tokens" },
                    new[] { "output_tokens" },
                    new[] { "cache_creation_input_tokens", "cache_read_input_tokens" });

            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("message", out var messageElement)
                && TryGetUsageElement(messageElement, out usageElement))
            {
                return ExtractUsage(
                    usageElement,
                    new[] { "input_tokens" },
                    new[] { "output_tokens" },
                    new[] { "cache_creation_input_tokens", "cache_read_input_tokens" });
            }

            return CopilotTokenUsage.Empty;
        }

        private static bool TryGetUsageElement(JsonElement element, out JsonElement usageElement)
        {
            usageElement = default;
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("usage", out usageElement)
                && usageElement.ValueKind == JsonValueKind.Object;
        }

        private static CopilotTokenUsage ExtractUsage(
            JsonElement usageElement,
            IReadOnlyList<string> inputKeys,
            IReadOnlyList<string> outputKeys,
            IReadOnlyList<string>? extraInputKeys = null)
        {
            var inputTokens = ReadFirstInt(usageElement, inputKeys);
            var outputTokens = ReadFirstInt(usageElement, outputKeys);

            if (extraInputKeys != null)
            {
                foreach (var key in extraInputKeys)
                    inputTokens += ReadFirstInt(usageElement, new[] { key });
            }

            var totalTokens = TryReadInt(usageElement, "total_tokens", out var total)
                ? total
                : Math.Max(0, inputTokens) + Math.Max(0, outputTokens);

            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens);
        }

        private static int ReadFirstInt(JsonElement element, IReadOnlyList<string> keys)
        {
            foreach (var key in keys)
            {
                if (TryReadInt(element, key, out var value))
                    return value;
            }

            return 0;
        }

        private static bool TryReadInt(JsonElement element, string propertyName, out int value)
        {
            value = 0;
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind == JsonValueKind.Number)
                return property.TryGetInt32(out value);

            if (property.ValueKind == JsonValueKind.String)
                return int.TryParse(property.GetString(), out value);

            return false;
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

        private static string ParseErrorMessage(string errorBody, int statusCode, string? apiKey)
        {
            string? detail = null;
            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                try
                {
                    using var document = JsonDocument.Parse(errorBody);
                    var root = document.RootElement;

                    if (TryExtractProviderErrorDetail(root, out var providerDetail))
                        detail = providerDetail;
                    else if (root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("message", out var topLevelMessage)
                        && topLevelMessage.ValueKind == JsonValueKind.String)
                    {
                        detail = topLevelMessage.GetString();
                    }
                }
                catch (JsonException)
                {
                }

                detail ??= errorBody;
            }

            var messageText = string.IsNullOrWhiteSpace(detail)
                ? $"Request failed, HTTP {statusCode}"
                : $"{statusCode}: {detail}";
            return CopilotUserFacingErrorFormatter.Sanitize(messageText, apiKey);
        }

        private static bool TryExtractProviderErrorDetail(JsonElement root, out string detail)
        {
            detail = string.Empty;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error))
                return false;

            if (error.ValueKind == JsonValueKind.String)
                detail = error.GetString() ?? string.Empty;
            else if (error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    detail = message.GetString() ?? string.Empty;
                else if (error.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
                    detail = type.GetString() ?? string.Empty;
            }

            return !string.IsNullOrWhiteSpace(detail);
        }

    }
}
