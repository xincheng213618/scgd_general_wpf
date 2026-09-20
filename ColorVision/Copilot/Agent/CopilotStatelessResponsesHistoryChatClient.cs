#pragma warning disable OPENAI001
#pragma warning disable SCME0001
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // The upstream streaming adapter keeps text deltas but not the completed message item.
    // Persist that item as message metadata so store=false history can replay its id and phase.
    internal sealed class CopilotStatelessResponsesHistoryChatClient : DelegatingChatClient
    {
        private const string ResponseMessageJsonKey = "ColorVision.OpenAI.Responses.MessageJson";
        private const string PlainReasoningItemIdKey = "ColorVision.OpenAI.Responses.PlainReasoningItemId";

        public CopilotStatelessResponsesHistoryChatClient(IChatClient innerClient)
            : base(innerClient)
        {
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.GetResponseAsync(
                PrepareMessages(messages),
                options,
                cancellationToken).ConfigureAwait(false);
            foreach (var message in response.Messages)
            {
                AddMessageHistoryMarker(message, message.RawRepresentation as MessageResponseItem);
                for (var index = 0; index < message.Contents.Count; index++)
                {
                    if (message.Contents[index] is TextReasoningContent { RawRepresentation: ReasoningResponseItem item } reasoning
                        && TryReadPlainReasoningText(item, out var text))
                        message.Contents[index] = MarkPlainReasoning(reasoning, text, item.Id);
                }
            }
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in base.GetStreamingResponseAsync(
                PrepareMessages(messages),
                options,
                cancellationToken).ConfigureAwait(false))
            {
                if (update.RawRepresentation is StreamingResponseReasoningTextDeltaUpdate plainReasoning)
                {
                    var preservedUpdate = update.Clone();
                    preservedUpdate.Contents = update.Contents.Select(content => content is TextReasoningContent reasoning
                        ? MarkPlainReasoning(reasoning, reasoning.Text, plainReasoning.ItemId) : content).ToList();
                    yield return preservedUpdate;
                    continue;
                }

                if (update.RawRepresentation is StreamingResponseOutputItemDoneUpdate
                    {
                        Item: MessageResponseItem messageItem,
                    })
                {
                    var preservedUpdate = update.Clone();
                    preservedUpdate.MessageId ??= messageItem.Id;
                    AddMessageHistoryMarker(preservedUpdate, messageItem);
                    yield return preservedUpdate;
                    continue;
                }

                yield return update;
            }
        }

        private static ChatMessage[] PrepareMessages(IEnumerable<ChatMessage> messages)
        {
            var materializedMessages = messages?.ToArray() ?? [];
            for (var index = 0; index < materializedMessages.Length; index++)
            {
                var message = materializedMessages[index];
                if (message.Role != ChatRole.Assistant)
                {
                    continue;
                }

                var hasMessageMarker = message.AdditionalProperties?.ContainsKey(ResponseMessageJsonKey) == true;
                var hasPlainReasoning = message.Contents.OfType<TextReasoningContent>()
                    .Any(content => content.AdditionalProperties?.ContainsKey(PlainReasoningItemIdKey) == true);
                if (!hasMessageMarker && !hasPlainReasoning)
                    continue;

                var preparedMessage = message.Clone();
                preparedMessage.AdditionalProperties = message.AdditionalProperties is null ? null : new(message.AdditionalProperties);
                preparedMessage.AdditionalProperties?.Remove(ResponseMessageJsonKey);
                preparedMessage.Contents = message.Contents.Select(content => content is TextReasoningContent reasoning
                    ? PreparePlainReasoning(reasoning) : content).ToList();
                // Portable message content is authoritative. Provider-private replay
                // metadata is used only while its text still describes that content.
                var responseItem = TryReadMessageHistoryMarker(message);
                if (responseItem is not null)
                {
                    preparedMessage.Contents = preparedMessage.Contents
                        .Where(content => content is not TextContent)
                        .ToList();
                    preparedMessage.Contents.Add(new AIContent { RawRepresentation = responseItem });
                }
                materializedMessages[index] = preparedMessage;
            }

            return materializedMessages;
        }

        private static TextReasoningContent MarkPlainReasoning(TextReasoningContent content, string? text, string? itemId)
        {
            var marked = new TextReasoningContent(text)
            {
                ProtectedData = content.ProtectedData,
                AdditionalProperties = content.AdditionalProperties is null ? new() : new(content.AdditionalProperties),
            };
            // Keep only the original kind and id, not a second copy of the text.
            // The portable content remains authoritative after editing or compaction.
            marked.AdditionalProperties[PlainReasoningItemIdKey] = itemId ?? string.Empty;
            return marked;
        }

        private static AIContent PreparePlainReasoning(TextReasoningContent content)
        {
            if (!string.IsNullOrEmpty(content.ProtectedData)
                || !TryGetHistoryString(content.AdditionalProperties, PlainReasoningItemIdKey, out var itemId))
                return content;
            var item = new Dictionary<string, object?>
            {
                ["type"] = "reasoning",
                ["summary"] = Array.Empty<object>(),
                ["content"] = new[] { new { type = "reasoning_text", text = content.Text ?? string.Empty } },
            };
            if (itemId.Length > 0) item["id"] = itemId;
            var prepared = MarkPlainReasoning(content, content.Text, itemId);
            prepared.RawRepresentation = ModelReaderWriter.Read<ReasoningResponseItem>(
                BinaryData.FromString(JsonSerializer.Serialize(item)), ModelReaderWriterOptions.Json);
            return prepared;
        }

        private static bool TryReadPlainReasoningText(ReasoningResponseItem item, [NotNullWhen(true)] out string? text)
        {
            text = null;
            if (!string.IsNullOrEmpty(item.EncryptedContent)) return false;
            using var document = JsonDocument.Parse(ModelReaderWriter.Write(item, ModelReaderWriterOptions.Json).ToString());
            if (!document.RootElement.TryGetProperty("content", out var parts)
                || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
                return false;
            var builder = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object
                    || !part.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "reasoning_text"
                    || !part.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String)
                    return false;
                builder.Append(value.GetString());
            }
            text = builder.ToString();
            return true;
        }

        private static void AddMessageHistoryMarker(
            ChatMessage message,
            MessageResponseItem? messageItem)
        {
            if (messageItem is not null
                && !HasMessageHistoryMarker(message.AdditionalProperties))
            {
                message.AdditionalProperties ??= new();
                message.AdditionalProperties[ResponseMessageJsonKey] = SerializeMessageItem(messageItem);
            }
        }

        private static void AddMessageHistoryMarker(
            ChatResponseUpdate update,
            MessageResponseItem messageItem)
        {
            update.AdditionalProperties = update.AdditionalProperties is null
                ? new()
                : new(update.AdditionalProperties);
            update.AdditionalProperties[ResponseMessageJsonKey] = SerializeMessageItem(messageItem);
        }

        private static bool HasMessageHistoryMarker(AdditionalPropertiesDictionary? properties) =>
            TryGetHistoryString(properties, ResponseMessageJsonKey, out _);

        private static string SerializeMessageItem(MessageResponseItem messageItem) =>
            ModelReaderWriter
                .Write(messageItem, ModelReaderWriterOptions.Json)
                .ToString();

        private static MessageResponseItem? TryReadMessageHistoryMarker(ChatMessage message)
        {
            if (!TryGetHistoryString(message.AdditionalProperties, ResponseMessageJsonKey, out var json))
                return null;

            try
            {
                var messageItem = ModelReaderWriter.Read<MessageResponseItem>(
                    BinaryData.FromString(json),
                    ModelReaderWriterOptions.Json);
                return messageItem?.Role == MessageRole.Assistant
                    && HasMatchingPortableText(message, json)
                    ? messageItem
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool HasMatchingPortableText(ChatMessage message, string json)
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array)
            {
                return !message.Contents.OfType<TextContent>().Any();
            }

            var markerTextParts = content
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("type", out var type)
                    && string.Equals(type.GetString(), "output_text", StringComparison.Ordinal)
                    && item.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                .Select(item => item.GetProperty("text").GetString() ?? string.Empty)
                .ToArray();
            var portableTextParts = message.Contents
                .OfType<TextContent>()
                .Select(content => content.Text ?? string.Empty)
                .ToArray();
            return markerTextParts.Length == 0
                ? portableTextParts.Length == 0
                : string.Equals(
                    string.Concat(markerTextParts),
                    string.Concat(portableTextParts),
                    StringComparison.Ordinal);
        }

        private static bool TryGetHistoryString(
            AdditionalPropertiesDictionary? properties,
            string key,
            [NotNullWhen(true)] out string? json)
        {
            json = null;
            if (properties?.TryGetValue(key, out var value) != true)
                return false;

            json = value switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null,
            };
            return json is not null;
        }
    }
}
