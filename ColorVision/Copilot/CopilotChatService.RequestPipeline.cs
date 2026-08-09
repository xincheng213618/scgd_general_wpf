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
        public async Task<CopilotChatReply> CompleteReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            CancellationToken cancellationToken)
        {
            var result = await CompleteReplyDetailedAsync(
                config,
                messages,
                imageAttachments: null,
                cancellationToken).ConfigureAwait(false);
            return result.Reply;
        }

        internal Task<CopilotCompletedReplyResult> CompleteReplyDetailedAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            CancellationToken cancellationToken) =>
            CompleteReplyDetailedAsync(config, messages, imageAttachments: null, cancellationToken);

        internal async Task<CopilotCompletedReplyResult> CompleteReplyDetailedAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            IReadOnlyList<CopilotAttachmentItem>? imageAttachments,
            CancellationToken cancellationToken)
        {
            var reasoningBuilder = new StringBuilder();
            var contentBuilder = new StringBuilder();
            var reasoningTruncated = false;
            var contentTruncated = false;

            var streamResult = await StreamReplyCoreAsync(
                config,
                messages,
                delta =>
                {
                    if (delta.HasReasoning)
                    {
                        AppendBoundedReplyText(
                            reasoningBuilder,
                            delta.ReasoningContent,
                            CopilotChatMessage.ReasoningTruncationMarker,
                            ref reasoningTruncated);
                    }

                    if (delta.HasContent)
                    {
                        AppendBoundedReplyText(
                            contentBuilder,
                            delta.Content,
                            CopilotChatMessage.ResponseTruncationMarker,
                            ref contentTruncated);
                    }
                },
                onRetry: null,
                onConnectionRecovery: null,
                onUsageChanged: null,
                imageAttachments: imageAttachments,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var reply = new CopilotChatReply(
                new CopilotStreamDelta(reasoningBuilder.ToString(), contentBuilder.ToString()),
                streamResult.Usage);
            return new CopilotCompletedReplyResult(reply, streamResult, contentTruncated);
        }

        private static void AppendBoundedReplyText(
            StringBuilder builder,
            string value,
            string truncationMarker,
            ref bool truncated)
        {
            if (truncated)
                return;

            var bounded = CopilotChatMessage.BoundAssistantDelta(
                builder.Length,
                value,
                truncationMarker,
                out truncated);
            builder.Append(bounded);
        }

        public Task<CopilotTokenUsage> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            CancellationToken cancellationToken) =>
            StreamReplyAsync(config, messages, onDelta, onRetry: null, imageAttachments: null, cancellationToken);

        internal Task<CopilotChatStreamResult> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotProviderRetryInfo>? onRetry,
            CancellationToken cancellationToken) =>
            StreamReplyCoreAsync(
                config,
                messages,
                onDelta,
                onRetry,
                onConnectionRecovery: null,
                onUsageChanged: null,
                imageAttachments: null,
                cancellationToken);

        internal Task<CopilotChatStreamResult> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotProviderRetryInfo>? onRetry,
            Action<CopilotTokenUsage>? onUsageChanged,
            CancellationToken cancellationToken) =>
            StreamReplyCoreAsync(
                config,
                messages,
                onDelta,
                onRetry,
                onConnectionRecovery: null,
                onUsageChanged,
                imageAttachments: null,
                cancellationToken);

        internal Task<CopilotChatStreamResult> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotProviderRetryInfo>? onRetry,
            Action<CopilotProviderConnectionRecoveryInfo> onConnectionRecovery,
            Action<CopilotTokenUsage>? onUsageChanged,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(onConnectionRecovery);
            return StreamReplyCoreAsync(
                config,
                messages,
                onDelta,
                onRetry,
                onConnectionRecovery,
                onUsageChanged,
                imageAttachments: null,
                cancellationToken);
        }

        internal async Task<CopilotTokenUsage> StreamReplyAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotProviderRetryInfo>? onRetry,
            IReadOnlyList<CopilotAttachmentItem>? imageAttachments,
            CancellationToken cancellationToken)
        {
            var result = await StreamReplyCoreAsync(
                config,
                messages,
                onDelta,
                onRetry,
                onConnectionRecovery: null,
                onUsageChanged: null,
                imageAttachments,
                cancellationToken).ConfigureAwait(false);
            return result.Usage;
        }

        private async Task<CopilotChatStreamResult> StreamReplyCoreAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotProviderRetryInfo>? onRetry,
            Action<CopilotProviderConnectionRecoveryInfo>? onConnectionRecovery,
            Action<CopilotTokenUsage>? onUsageChanged,
            IReadOnlyList<CopilotAttachmentItem>? imageAttachments,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(messages);
            ArgumentNullException.ThrowIfNull(onDelta);
            var requestMessages = CopilotRequestMessageSequence.Normalize(messages);
            if (requestMessages.Length == 0)
                throw new InvalidOperationException("At least one non-empty user or assistant message is required.");
            var imagePayloads = await CopilotImagePayloadLoader.LoadAsync(imageAttachments, cancellationToken).ConfigureAwait(false);
            var inactivityTimeouts = CopilotProviderInactivityPolicy.Resolve(
                config,
                _firstResponseTimeoutOverride,
                _streamingUpdateTimeoutOverride);

            var attempt = 1;
            var connectionRecoveryState = new CopilotProviderConnectionRecoveryState(
                CopilotProviderConnectionRecoveryChatClient.DefaultInitialDelay,
                CopilotProviderConnectionRecoveryChatClient.DefaultMaximumDelay);
            while (true)
            {
                var responseStarted = false;
                try
                {
                    var result = await StreamReplyAttemptAsync(
                        config,
                        requestMessages,
                        imagePayloads,
                        delta =>
                        {
                            responseStarted = true;
                            onDelta(delta);
                        },
                        usage =>
                        {
                            responseStarted = true;
                            onUsageChanged?.Invoke(usage);
                        },
                        inactivityTimeouts,
                        cancellationToken).ConfigureAwait(false);
                    return result with
                    {
                        ImagePreparationNotice = BuildImagePreparationNotice(imagePayloads),
                    };
                }
                catch (Exception exception) when (onConnectionRecovery != null
                    && !responseStarted
                    && CopilotProviderConnectionRecoveryChatClient.TryCreateRecovery(
                        exception,
                        connectionRecoveryState,
                        cancellationToken,
                        out var recovery))
                {
                    onConnectionRecovery(recovery);
                    await _delayAsync(recovery.Delay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (TryCreateRetry(exception, attempt, responseStarted, cancellationToken, out var retry))
                {
                    onRetry?.Invoke(retry);
                    await _delayAsync(retry.Delay, cancellationToken).ConfigureAwait(false);
                    attempt++;
                }
            }
        }

        private async Task<CopilotChatStreamResult> StreamReplyAttemptAsync(
            CopilotProfileConfig config,
            IReadOnlyList<CopilotRequestMessage> messages,
            IReadOnlyList<CopilotImagePayload> imagePayloads,
            Action<CopilotStreamDelta> onDelta,
            Action<CopilotTokenUsage> onUsageChanged,
            CopilotProviderInactivityTimeouts inactivityTimeouts,
            CancellationToken cancellationToken)
        {
            using var request = CreateRequest(config, messages, imagePayloads);
            var firstResponseStopwatch = Stopwatch.StartNew();
            using var response = await SendResponseHeadersAsync(
                request,
                inactivityTimeouts,
                cancellationToken).ConfigureAwait(false);
            CopilotProviderRateLimitTracker.Capture(config.Id, response);
            var remainingFirstResponseTimeout = SubtractElapsed(
                inactivityTimeouts.FirstResponseTimeout,
                firstResponseStopwatch.Elapsed);
            var providerRequestId = CopilotProviderRequestId.Redact(
                CopilotProviderRequestId.Extract(response),
                config.ApiKey);

            var statusCode = (int)response.StatusCode;
            if (statusCode is >= 300 and < 400)
            {
                var redirectException = new InvalidOperationException(
                    CopilotProviderRequestId.AppendToMessage(
                        $"HTTP {statusCode}: The provider redirected the request. Redirects are disabled to prevent sending API keys or prompt content to another location. Configure the final API base URL directly.",
                        providerRequestId));
                redirectException.Data[ProviderStatusCodeDataKey] = statusCode;
                CopilotProviderRequestId.Preserve(
                    redirectException,
                    providerRequestId);
                throw redirectException;
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorBody;
                try
                {
                    errorBody = await ReadBoundedContentWithTimeoutAsync(
                        response,
                        MaximumProviderErrorResponseBytes,
                        "Provider error response",
                        remainingFirstResponseTimeout,
                        CopilotProviderInactivityPhase.FirstResponse,
                        inactivityTimeouts,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (CopilotHttpContentSizeLimitException exception)
                {
                    var oversizedResponseException = new InvalidOperationException(
                        CopilotProviderRequestId.AppendToMessage(
                            $"{(int)response.StatusCode}: {exception.Message}",
                            providerRequestId),
                        exception);
                    oversizedResponseException.Data[ProviderStatusCodeDataKey] = (int)response.StatusCode;
                    CopilotProviderRequestId.Preserve(
                        oversizedResponseException,
                        providerRequestId);
                    CopilotProviderRetryChatClient.PreserveRetryAfter(response, oversizedResponseException);
                    throw oversizedResponseException;
                }
                catch (CopilotProviderInactivityException exception)
                {
                    exception.Data[ProviderStatusCodeDataKey] = statusCode;
                    CopilotProviderRequestId.Preserve(
                        exception,
                        providerRequestId);
                    CopilotProviderRetryChatClient.PreserveRetryAfter(response, exception);
                    throw;
                }

                var providerException = new InvalidOperationException(ParseErrorMessage(
                    errorBody,
                    (int)response.StatusCode,
                    config.ApiKey,
                    providerRequestId,
                    out var errorRequestId));
                providerException.Data[ProviderStatusCodeDataKey] = (int)response.StatusCode;
                CopilotProviderRequestId.Preserve(
                    providerException,
                    errorRequestId);
                CopilotProviderRetryChatClient.PreserveRetryAfter(response, providerException);
                throw providerException;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return await ReadStreamingResponseAsync(
                        config,
                        response,
                        onDelta,
                        onUsageChanged,
                        remainingFirstResponseTimeout,
                        inactivityTimeouts,
                        providerRequestId,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    CopilotProviderRequestId.Preserve(
                        exception,
                        providerRequestId);
                    throw;
                }
            }

            string body;
            try
            {
                body = await ReadBoundedContentWithTimeoutAsync(
                    response,
                    MaximumNonStreamingResponseBytes,
                    "Non-streaming provider response",
                    remainingFirstResponseTimeout,
                    CopilotProviderInactivityPhase.FirstResponse,
                    inactivityTimeouts,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                CopilotProviderRequestId.Preserve(
                    exception,
                    providerRequestId);
                throw;
            }
            if (TryCreateProviderPayloadException(
                body,
                "Provider response",
                config.ApiKey,
                providerRequestId,
                out var payloadException))
            {
                throw payloadException;
            }

            var reply = ExtractFinalResponseReply(config.ProviderType, body);
            if (!reply.Delta.HasAny)
            {
                throw new InvalidOperationException(
                    CopilotProviderRequestId.AppendToMessage(
                        "The API returned successfully, but no displayable text was found.",
                        providerRequestId));
            }

            onDelta(reply.Delta);
            if (reply.Usage.HasAny)
                onUsageChanged(reply.Usage);
            var finishReason = ExtractProviderFinishReason(config.ProviderType, body);
            return CreateStreamResult(reply.Usage, finishReason);
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
            if (TryFindProviderPayloadException(exception, out var payloadException))
            {
                statusCode = null;
                failureKind = string.IsNullOrWhiteSpace(payloadException.ErrorCode)
                    ? "provider error"
                    : payloadException.ErrorCode;
                if (!payloadException.IsTransient)
                    return false;
            }
            else if (TryGetProviderStatusCode(exception, out var providerStatusCode))
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
                CopilotProviderRetryChatClient.ResolveRetryDelay(exception, _retryDelayFactory(failedAttempt)),
                failureKind,
                statusCode,
                CopilotProviderRequestId.Find(exception));
            return true;
        }

        private static bool TryFindProviderPayloadException(
            Exception exception,
            out CopilotProviderPayloadException payloadException)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is CopilotProviderPayloadException candidate)
                {
                    payloadException = candidate;
                    return true;
                }
            }

            payloadException = null!;
            return false;
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
            var request = new HttpRequestMessage(HttpMethod.Post, CopilotProviderEndpoint.Build(config));
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
                        role = CopilotOpenAiRequestPolicy.GetInstructionRole(config),
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
                    ["stream_options"] = new
                    {
                        include_usage = true,
                    },
                    ["messages"] = payloadMessages,
                };
                payload[CopilotOpenAiRequestPolicy
                    .GetMaximumOutputTokensPropertyName(config)] =
                    config.MaxTokens;

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
            var preparationNotice = BuildImagePreparationNotice(images);
            if (preparationNotice.Length > 0)
                content.Add(new { type = "text", text = preparationNotice });
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
            var preparationNotice = BuildImagePreparationNotice(images);
            if (preparationNotice.Length > 0)
                content.Add(new { type = "text", text = preparationNotice });
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

        internal static string BuildImagePreparationNotice(IReadOnlyList<CopilotImagePayload> images)
        {
            var resized = images.Where(image => image.WasResized).ToArray();
            if (resized.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append("[Image preparation] The application resized ")
                .Append(resized.Length)
                .Append(" image(s) to fit the ")
                .Append(CopilotImageInputBudget.MaximumDimension)
                .Append(" px / ")
                .Append(CopilotImageInputBudget.MaximumPatches)
                .AppendLine(" patch input budget. Only the prepared pixels are available to this vision pass:");
            foreach (var image in resized)
            {
                builder.Append("- ")
                    .Append(image.DisplayLabel)
                    .Append(": ")
                    .Append(image.SourceWidth)
                    .Append('×')
                    .Append(image.SourceHeight)
                    .Append(" -> ")
                    .Append(image.PreparedWidth)
                    .Append('×')
                    .AppendLine(image.PreparedHeight.ToString());
            }
            return builder.ToString().TrimEnd();
        }

    }
}
