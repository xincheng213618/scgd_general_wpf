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
    public sealed partial class CopilotChatService
    {
        private async Task<CopilotChatStreamResult> ReadStreamingResponseAsync(
            CopilotProfileConfig config,
            HttpResponseMessage response,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotTokenUsage> onUsageChanged,
            TimeSpan remainingFirstResponseTimeout,
            CopilotProviderInactivityTimeouts inactivityTimeouts,
            string providerRequestId,
            CancellationToken cancellationToken)
        {
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                if (state is HttpResponseMessage message)
                    message.Dispose();
            }, response);

            var streamOpenStopwatch = Stopwatch.StartNew();
            await using var stream = await ReadResponseStreamWithTimeoutAsync(
                response,
                remainingFirstResponseTimeout,
                CopilotProviderInactivityPhase.FirstResponse,
                inactivityTimeouts,
                cancellationToken).ConfigureAwait(false);
            var remainingInactivityTimeout = SubtractElapsed(
                remainingFirstResponseTimeout,
                streamOpenStopwatch.Elapsed);
            using var reader = new CopilotBoundedTextLineReader(
                stream,
                Encoding.UTF8,
                MaximumStreamingResponseBytes,
                MaximumStreamingLineCharacters,
                "Provider event stream");
            var eventData = new StringBuilder();
            var usage = CopilotTokenUsage.Empty;
            var receivedDisplayableText = false;
            var streamCompleted = false;
            var finishReason = string.Empty;

            bool ProcessPendingEvent()
            {
                var emittedContent = false;
                var completed = ProcessStreamingEventData(
                    config,
                    eventData,
                    providerRequestId,
                    delta =>
                    {
                        emittedContent = true;
                        onDelta(delta);
                    },
                    onUsageChanged,
                    ref usage,
                    ref receivedDisplayableText,
                    ref finishReason);
                if (emittedContent)
                    remainingInactivityTimeout =
                        inactivityTimeouts.StreamingUpdateTimeout;
                return completed;
            }

            while (!streamCompleted)
            {
                var inactivityPhase = receivedDisplayableText
                    ? CopilotProviderInactivityPhase.StreamingUpdate
                    : CopilotProviderInactivityPhase.FirstResponse;
                var readStopwatch = Stopwatch.StartNew();
                string? line;
                try
                {
                    line = await ReadProviderLineWithTimeoutAsync(
                        reader,
                        response,
                        remainingInactivityTimeout,
                        inactivityPhase,
                        inactivityTimeouts,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (IOException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                remainingInactivityTimeout = SubtractElapsed(
                    remainingInactivityTimeout,
                    readStopwatch.Elapsed);

                if (line is null)
                {
                    streamCompleted = ProcessPendingEvent();
                    break;
                }

                if (line.Length == 0)
                {
                    streamCompleted = ProcessPendingEvent();
                    continue;
                }

                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var data = line[5..];
                if (data.StartsWith(' '))
                    data = data[1..];
                if (eventData.Length > 0)
                    eventData.Append('\n');
                eventData.Append(data);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!receivedDisplayableText)
            {
                throw new InvalidOperationException(
                    CopilotProviderRequestId.AppendToMessage(
                        "The API stream completed successfully, but no displayable text was found.",
                        providerRequestId));
            }
            if (!streamCompleted && string.IsNullOrWhiteSpace(finishReason))
            {
                var exception = new IOException(
                    CopilotProviderRequestId.AppendToMessage(
                        "The provider stream ended before a completion event or finish reason was received.",
                        providerRequestId));
                CopilotProviderRequestId.Preserve(
                    exception,
                    providerRequestId);
                throw exception;
            }

            return CreateStreamResult(usage, finishReason);
        }

        private static bool ProcessStreamingEventData(
            CopilotProfileConfig config,
            StringBuilder eventData,
            string providerRequestId,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotTokenUsage> onUsageChanged,
            ref CopilotTokenUsage usage,
            ref bool receivedDisplayableText,
            ref string finishReason)
        {
            if (eventData.Length == 0)
                return false;

            var payload = eventData.ToString().Trim();
            eventData.Clear();
            if (payload.Length == 0)
                return false;
            if (string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
                return true;
            if (TryCreateProviderPayloadException(
                payload,
                "Provider stream",
                config.ApiKey,
                providerRequestId,
                out var payloadException))
            {
                throw payloadException;
            }

            var terminalEvent = IsTerminalStreamingEvent(config.ProviderType, payload);
            var reportedFinishReason = ExtractProviderFinishReason(config.ProviderType, payload);
            if (!string.IsNullOrWhiteSpace(reportedFinishReason))
                finishReason = reportedFinishReason;

            var reply = config.ProviderType == CopilotProviderType.AnthropicCompatible
                ? ExtractAnthropicStreamingReply(payload)
                : ExtractOpenAiStreamingReply(payload);
            if (reply.Usage.HasAny)
            {
                var updatedUsage = usage.MergeProgress(reply.Usage);
                if (updatedUsage != usage)
                {
                    usage = updatedUsage;
                    onUsageChanged(usage);
                }
            }
            if (!reply.Delta.HasAny)
                return terminalEvent;

            receivedDisplayableText = true;
            onDelta(reply.Delta);
            return terminalEvent;
        }

        private static CopilotChatStreamResult CreateStreamResult(CopilotTokenUsage usage, string? finishReason)
        {
            var normalizedReason = NormalizeFinishReason(finishReason);
            return new CopilotChatStreamResult(
                usage,
                CopilotProviderFinishReasonClassifier.Classify(normalizedReason),
                normalizedReason);
        }

        private static string ExtractProviderFinishReason(CopilotProviderType providerType, string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return string.Empty;

            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (TryReadFinishReason(root, out var finishReason))
                    return finishReason;

                if (providerType == CopilotProviderType.OpenAICompatible
                    && root.TryGetProperty("choices", out var choices)
                    && choices.ValueKind == JsonValueKind.Array
                    && choices.GetArrayLength() > 0
                    && TryReadFinishReason(choices[0], out finishReason))
                {
                    return finishReason;
                }

                foreach (var propertyName in new[] { "delta", "message" })
                {
                    if (root.TryGetProperty(propertyName, out var nested)
                        && TryReadFinishReason(nested, out finishReason))
                    {
                        return finishReason;
                    }
                }
            }
            catch (JsonException)
            {
            }

            return string.Empty;
        }

        private static bool TryReadFinishReason(JsonElement element, out string finishReason)
        {
            finishReason = string.Empty;
            if (element.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var propertyName in new[] { "finish_reason", "stop_reason" })
            {
                if (element.TryGetProperty(propertyName, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    finishReason = value.GetString()!;
                    return true;
                }
            }

            if (element.TryGetProperty("finish_details", out var details)
                && details.ValueKind == JsonValueKind.Object
                && details.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(type.GetString()))
            {
                finishReason = type.GetString()!;
                return true;
            }

            return false;
        }

        private static bool IsTerminalStreamingEvent(CopilotProviderType providerType, string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("type", out var typeElement)
                    || typeElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var type = typeElement.GetString();
                return providerType == CopilotProviderType.AnthropicCompatible
                    ? string.Equals(type, "message_stop", StringComparison.OrdinalIgnoreCase)
                    : string.Equals(type, "response.completed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(type, "response.done", StringComparison.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string NormalizeFinishReason(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(Math.Min(value.Length, 64));
            foreach (var character in value.Trim())
            {
                if (char.IsControl(character))
                    continue;
                builder.Append(character);
                if (builder.Length == 64)
                    break;
            }
            return builder.ToString().Trim();
        }

        private static bool TryCreateProviderPayloadException(
            string payload,
            string sourceLabel,
            string? apiKey,
            string fallbackRequestId,
            out CopilotProviderPayloadException exception)
        {
            exception = null!;
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (!TryExtractProviderPayloadError(root, out var providerError))
                    return false;

                var errorCode = NormalizeProviderErrorCode(providerError.Code);
                var codeSuffix = string.IsNullOrWhiteSpace(errorCode)
                    ? string.Empty
                    : $" ({errorCode})";
                var requestId = CopilotProviderRequestId.Redact(
                    CopilotProviderRequestId.Prefer(
                        providerError.RequestId,
                        fallbackRequestId),
                    apiKey);
                var message = CopilotUserFacingErrorFormatter.Sanitize(
                    CopilotProviderRequestId.AppendToMessage(
                        $"{sourceLabel} error{codeSuffix}: {providerError.Message}",
                        requestId),
                    apiKey);
                exception = new CopilotProviderPayloadException(
                    message,
                    errorCode,
                    IsTransientProviderErrorCode(errorCode),
                    requestId);
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

    }
}
