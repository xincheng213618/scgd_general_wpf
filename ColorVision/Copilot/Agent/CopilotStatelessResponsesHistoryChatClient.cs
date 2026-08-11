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
                AddMessageHistoryMarker(message, message.RawRepresentation as MessageResponseItem);
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

                if (message.AdditionalProperties?.ContainsKey(ResponseMessageJsonKey) != true)
                    continue;

                var preparedMessage = message.Clone();
                preparedMessage.AdditionalProperties = new(message.AdditionalProperties);
                preparedMessage.AdditionalProperties.Remove(ResponseMessageJsonKey);
                var responseItem = TryReadMessageHistoryMarker(message);
                if (responseItem is not null)
                {
                    preparedMessage.Contents = message.Contents
                        .Where(content => content is not TextContent)
                        .ToList();
                    preparedMessage.Contents.Add(new AIContent { RawRepresentation = responseItem });
                }
                materializedMessages[index] = preparedMessage;
            }

            return materializedMessages;
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
            TryGetMessageHistoryJson(properties, out _);

        private static string SerializeMessageItem(MessageResponseItem messageItem) =>
            ModelReaderWriter
                .Write(messageItem, ModelReaderWriterOptions.Json)
                .ToString();

        private static MessageResponseItem? TryReadMessageHistoryMarker(ChatMessage message)
        {
            if (!TryGetMessageHistoryJson(message.AdditionalProperties, out var json))
                return null;

            try
            {
                var messageItem = ModelReaderWriter.Read<MessageResponseItem>(
                    BinaryData.FromString(json),
                    ModelReaderWriterOptions.Json);
                return messageItem?.Role == MessageRole.Assistant
                    ? messageItem
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TryGetMessageHistoryJson(
            AdditionalPropertiesDictionary? properties,
            [NotNullWhen(true)] out string? json)
        {
            json = null;
            if (properties?.TryGetValue(ResponseMessageJsonKey, out var value) != true)
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
