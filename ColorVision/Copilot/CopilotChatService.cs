#pragma warning disable CA1822,CA1861
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    internal enum CopilotChatFinishKind
    {
        Unspecified,
        Complete,
        LengthLimit,
        ContentFiltered,
        ToolRequested,
        Other,
    }

    internal readonly record struct CopilotChatStreamResult(
        CopilotTokenUsage Usage,
        CopilotChatFinishKind FinishKind,
        string FinishReason)
    {
        public bool IsIncomplete => FinishKind is CopilotChatFinishKind.LengthLimit
            or CopilotChatFinishKind.ContentFiltered
            or CopilotChatFinishKind.ToolRequested
            or CopilotChatFinishKind.Other;
    }

    internal readonly record struct CopilotCompletedReplyResult(
        CopilotChatReply Reply,
        CopilotChatStreamResult StreamResult,
        bool IsContentTruncated)
    {
        public bool IsIncomplete => IsContentTruncated || StreamResult.IsIncomplete;

        public string Content => Reply.Content;

        public CopilotTokenUsage Usage => Reply.Usage;
    }

    internal sealed class CopilotProviderPayloadException : InvalidOperationException
    {
        public CopilotProviderPayloadException(
            string message,
            string errorCode,
            bool isTransient,
            string requestId)
            : base(message)
        {
            ErrorCode = errorCode ?? string.Empty;
            IsTransient = isTransient;
            RequestId = CopilotProviderRequestId.Normalize(requestId);
            CopilotProviderRequestId.Preserve(this, RequestId);
        }

        public string ErrorCode { get; }

        public bool IsTransient { get; }

        public string RequestId { get; }
    }

    public sealed partial class CopilotChatService
    {
        private const int MaximumProviderErrorResponseBytes = 256 * 1024;
        private const int MaximumNonStreamingResponseBytes = 4 * 1024 * 1024;
        private const int MaximumStreamingResponseBytes = 8 * 1024 * 1024;
        private const int MaximumStreamingLineCharacters = 1024 * 1024;
        private const string ProviderStatusCodeDataKey = "ColorVision.Copilot.ProviderStatusCode";
        private static readonly HttpClient SharedHttpClient = CopilotProviderHttpTransport.CreateClient();
        private readonly HttpClient _httpClient;
        private readonly int _maximumAttempts;
        private readonly Func<int, TimeSpan> _retryDelayFactory;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly TimeSpan? _firstResponseTimeoutOverride;
        private readonly TimeSpan? _streamingUpdateTimeoutOverride;

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
            Func<TimeSpan, CancellationToken, Task> delayAsync,
            TimeSpan? firstResponseTimeout = null,
            TimeSpan? streamingUpdateTimeout = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
            _maximumAttempts = maximumAttempts;
            _retryDelayFactory = retryDelayFactory ?? throw new ArgumentNullException(nameof(retryDelayFactory));
            _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
            _firstResponseTimeoutOverride = firstResponseTimeout;
            _streamingUpdateTimeoutOverride = streamingUpdateTimeout;
            if (firstResponseTimeout.HasValue)
            {
                CopilotProviderInactivityPolicy.ValidateTimeout(
                    firstResponseTimeout.Value,
                    nameof(firstResponseTimeout));
            }
            if (streamingUpdateTimeout.HasValue)
            {
                CopilotProviderInactivityPolicy.ValidateTimeout(
                    streamingUpdateTimeout.Value,
                    nameof(streamingUpdateTimeout));
            }
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

        internal static CopilotTokenUsage ExtractOpenAiUsage(JsonElement element)
        {
            if (!TryGetUsageElement(element, out var usageElement))
                return CopilotTokenUsage.Empty;

            var usage = ExtractUsage(
                usageElement,
                new[] { "prompt_tokens", "input_tokens" },
                new[] { "completion_tokens", "output_tokens" });
            return usage with { CachedInputTokens = ReadOpenAiCachedInputTokens(usageElement) };
        }

        internal static CopilotTokenUsage ExtractAnthropicUsage(JsonElement element)
        {
            if (TryGetUsageElement(element, out var usageElement))
                return ExtractUsage(
                    usageElement,
                    new[] { "input_tokens" },
                    new[] { "output_tokens" },
                    new[] { "cache_creation_input_tokens", "cache_read_input_tokens" },
                    "cache_read_input_tokens");

            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("message", out var messageElement)
                && TryGetUsageElement(messageElement, out usageElement))
            {
                return ExtractUsage(
                    usageElement,
                    new[] { "input_tokens" },
                    new[] { "output_tokens" },
                    new[] { "cache_creation_input_tokens", "cache_read_input_tokens" },
                    "cache_read_input_tokens");
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
            IReadOnlyList<string>? extraInputKeys = null,
            string? cachedInputKey = null)
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

            int? cachedInputTokens = null;
            if (!string.IsNullOrWhiteSpace(cachedInputKey)
                && TryReadInt(usageElement, cachedInputKey, out var cached))
            {
                cachedInputTokens = cached;
            }

            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens, cachedInputTokens);
        }

        private static int? ReadOpenAiCachedInputTokens(JsonElement usageElement)
        {
            foreach (var detailsKey in new[] { "prompt_tokens_details", "input_tokens_details" })
            {
                if (usageElement.TryGetProperty(detailsKey, out var details)
                    && TryReadInt(details, "cached_tokens", out var cachedTokens))
                {
                    return cachedTokens;
                }
            }

            return null;
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

        private static string ParseErrorMessage(
            string errorBody,
            int statusCode,
            string? apiKey,
            string fallbackRequestId,
            out string requestId)
        {
            string? detail = null;
            requestId = CopilotProviderRequestId.Redact(
                fallbackRequestId,
                apiKey);
            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                try
                {
                    using var document = JsonDocument.Parse(errorBody);
                    var root = document.RootElement;
                    requestId = CopilotProviderRequestId.Redact(
                        CopilotProviderRequestId.Prefer(
                            CopilotProviderRequestId.Extract(root),
                            requestId),
                        apiKey);

                    if (TryExtractProviderPayloadError(root, out var providerError))
                    {
                        detail = providerError.Message;
                    }
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
            return CopilotUserFacingErrorFormatter.Sanitize(
                CopilotProviderRequestId.AppendToMessage(
                    messageText,
                    requestId),
                apiKey);
        }

        private static bool TryExtractProviderPayloadError(
            JsonElement root,
            out ProviderPayloadError providerError)
        {
            providerError = default;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            var requestId = CopilotProviderRequestId.Extract(root);

            if (root.TryGetProperty("error", out var error)
                && TryReadProviderErrorElement(error, out providerError))
            {
                providerError = providerError with { RequestId = requestId };
                return true;
            }

            if (TryGetString(root, "type", out var eventType)
                && string.Equals(eventType, "error", StringComparison.OrdinalIgnoreCase)
                && TryGetString(root, "message", out var eventMessage))
            {
                TryGetString(root, "code", out var eventCode);
                providerError = new ProviderPayloadError(
                    eventCode,
                    eventMessage,
                    requestId);
                return true;
            }

            if (TryGetString(root, "type", out eventType)
                && string.Equals(eventType, "response.failed", StringComparison.OrdinalIgnoreCase)
                && root.TryGetProperty("response", out var response)
                && response.ValueKind == JsonValueKind.Object
                && response.TryGetProperty("error", out error)
                && TryReadProviderErrorElement(error, out providerError))
            {
                providerError = providerError with { RequestId = requestId };
                return true;
            }

            return false;
        }

        private static bool TryReadProviderErrorElement(
            JsonElement error,
            out ProviderPayloadError providerError)
        {
            providerError = default;
            if (error.ValueKind == JsonValueKind.String)
            {
                var stringErrorMessage = error.GetString();
                if (string.IsNullOrWhiteSpace(stringErrorMessage))
                    return false;

                providerError = new ProviderPayloadError(
                    string.Empty,
                    stringErrorMessage,
                    string.Empty);
                return true;
            }

            if (error.ValueKind != JsonValueKind.Object)
                return false;

            TryGetString(error, "type", out var errorType);
            TryGetString(error, "code", out var errorCode);
            var code = string.IsNullOrWhiteSpace(errorType) ? errorCode : errorType;
            if (!TryGetString(error, "message", out var message))
                message = code;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            providerError = new ProviderPayloadError(
                code,
                message,
                string.Empty);
            return true;
        }

        private static bool TryGetString(
            JsonElement element,
            string propertyName,
            out string value)
        {
            value = string.Empty;
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(property.GetString()))
            {
                return false;
            }

            value = property.GetString()!;
            return true;
        }

        private static string NormalizeProviderErrorCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(Math.Min(value.Length, 64));
            foreach (var character in value.Trim())
            {
                if (char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
                    builder.Append(character);
                if (builder.Length == 64)
                    break;
            }
            return builder.ToString();
        }

        private static bool IsTransientProviderErrorCode(string errorCode)
        {
            var comparable = errorCode
                .Replace('-', '_')
                .Replace('.', '_')
                .ToLowerInvariant();
            return comparable is "overloaded_error"
                or "rate_limit_error"
                or "rate_limit_exceeded"
                or "api_error"
                or "server_error"
                or "timeout_error"
                or "service_unavailable";
        }

        private readonly record struct ProviderPayloadError(
            string Code,
            string Message,
            string RequestId);

    }
}
