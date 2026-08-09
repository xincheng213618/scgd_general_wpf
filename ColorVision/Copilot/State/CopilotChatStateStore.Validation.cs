using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatStateStore
    {
        private static StateFileReadStatus ReadStateFile(
            string filePath,
            out CopilotChatState state,
            out int schemaVersion)
        {
            state = new CopilotChatState();
            schemaVersion = 0;
            if (!File.Exists(filePath))
                return StateFileReadStatus.Missing;

            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 8192,
                    FileOptions.SequentialScan);
                if (stream.Length > MaximumStateFileBytes)
                    return StateFileReadStatus.Invalid;

                using var textReader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 8192,
                    leaveOpen: false);
                using var jsonReader = new JsonTextReader(textReader) { CloseInput = false };
                if (JToken.Load(jsonReader) is not JObject document || jsonReader.Read())
                    return StateFileReadStatus.Invalid;

                var schemaToken = document.GetValue(nameof(CopilotChatState.SchemaVersion), StringComparison.OrdinalIgnoreCase);
                if (schemaToken != null)
                {
                    if (schemaToken.Type != JTokenType.Integer)
                        return StateFileReadStatus.Invalid;

                    var parsedSchemaVersion = schemaToken.Value<long>();
                    if (parsedSchemaVersion > int.MaxValue)
                    {
                        schemaVersion = int.MaxValue;
                        return StateFileReadStatus.FutureVersion;
                    }
                    if (parsedSchemaVersion < 1)
                        return StateFileReadStatus.Invalid;

                    schemaVersion = (int)parsedSchemaVersion;
                    if (schemaVersion > CopilotChatState.CurrentSchemaVersion)
                        return StateFileReadStatus.FutureVersion;
                }

                if (!HasTrustedDocumentShape(document))
                    return StateFileReadStatus.Invalid;

                var deserializedState = document.ToObject<CopilotChatState>(JsonSerializer.Create(SerializerSettings));
                if (deserializedState == null)
                    return StateFileReadStatus.Invalid;

                state = deserializedState;
                state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
                return StateFileReadStatus.Valid;
            }
            catch
            {
                return StateFileReadStatus.Invalid;
            }
        }

        private static void ValidateSerializedStateSize(string serializedState)
        {
            var actualBytes = Encoding.UTF8.GetByteCount(serializedState);
            if (actualBytes <= MaximumStateFileBytes)
                return;

            throw new CopilotChatStateSizeLimitException(actualBytes, MaximumStateFileBytes);
        }

        private static bool HasTrustedDocumentShape(JObject document)
        {
            if (!IsStringOrNull(document.GetValue(nameof(CopilotChatState.ActiveConversationId), StringComparison.OrdinalIgnoreCase))
                || !IsStringOrNull(document.GetValue(nameof(CopilotChatState.ActiveProfileId), StringComparison.OrdinalIgnoreCase))
                || !IsOptionalBoolean(document.GetValue(nameof(CopilotChatState.IsAgentTaskPanelExpanded), StringComparison.OrdinalIgnoreCase))
                || !IsOptionalBoolean(document.GetValue(nameof(CopilotChatState.ShowMessageTimestamps), StringComparison.OrdinalIgnoreCase))
                || !IsOptionalBoolean(document.GetValue(nameof(CopilotChatState.UseCompactMessageLayout), StringComparison.OrdinalIgnoreCase))
                || !IsOptionalBoolean(document.GetValue(nameof(CopilotChatState.EnablePromptHistoryCompletions), StringComparison.OrdinalIgnoreCase))
                || !IsOptionalBoolean(document.GetValue(nameof(CopilotChatState.UseMultilineComposer), StringComparison.OrdinalIgnoreCase))
                || !IsOptionalInteger(document.GetValue(nameof(CopilotChatState.DefaultFollowUpBehavior), StringComparison.OrdinalIgnoreCase))
                || document.GetValue(nameof(CopilotChatState.Conversations), StringComparison.OrdinalIgnoreCase) is not JArray conversations)
            {
                return false;
            }

            foreach (var conversationToken in conversations)
            {
                if (conversationToken is not JObject conversation
                    || conversation.GetValue(nameof(CopilotConversationRecord.Messages), StringComparison.OrdinalIgnoreCase) is not JArray messages
                    || conversation.GetValue(nameof(CopilotConversationRecord.Attachments), StringComparison.OrdinalIgnoreCase) is not JArray attachments
                    || !IsOptionalString(conversation.GetValue(nameof(CopilotConversationRecord.DraftText), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalInteger(conversation.GetValue(nameof(CopilotConversationRecord.DraftRequestMode), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalReviewTarget(conversation.GetValue(nameof(CopilotConversationRecord.DraftWorkspaceReviewTarget), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalSkillReference(conversation.GetValue(nameof(CopilotConversationRecord.DraftAgentSkillReference), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalComposerStash(conversation.GetValue(nameof(CopilotConversationRecord.ComposerStash), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalPendingSteeringRecoveries(conversation.GetValue(nameof(CopilotConversationRecord.PendingSteeringRecoveries), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalObject(conversation.GetValue(nameof(CopilotConversationRecord.BranchOrigin), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalBoolean(conversation.GetValue(nameof(CopilotConversationRecord.IsGoalContinuationDeferred), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalDate(conversation.GetValue(nameof(CopilotConversationRecord.RecencyAt), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalBoolean(conversation.GetValue(nameof(CopilotConversationRecord.IsArchived), StringComparison.OrdinalIgnoreCase))
                    || messages.Any(item => item is not JObject)
                    || attachments.Any(item => item is not JObject))
                {
                    return false;
                }
            }

            var recoveryToken = document.GetValue(nameof(CopilotChatState.QueuedFollowUpRecoveries), StringComparison.OrdinalIgnoreCase);
            if (recoveryToken != null && recoveryToken.Type != JTokenType.Null)
            {
                if (recoveryToken is not JArray recoveries)
                    return false;

                foreach (var recoveryTokenItem in recoveries)
                {
                    if (recoveryTokenItem is not JObject recovery
                        || !IsOptionalString(recovery.GetValue(nameof(CopilotQueuedFollowUpRecoveryRecord.RunId), StringComparison.OrdinalIgnoreCase))
                        || !IsOptionalString(recovery.GetValue(nameof(CopilotQueuedFollowUpRecoveryRecord.ConversationId), StringComparison.OrdinalIgnoreCase))
                        || !IsOptionalString(recovery.GetValue(nameof(CopilotQueuedFollowUpRecoveryRecord.Prompt), StringComparison.OrdinalIgnoreCase))
                        || !IsOptionalComposerStash(recovery.GetValue(nameof(CopilotQueuedFollowUpRecoveryRecord.ComposerState), StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsStringOrNull(JToken? token) => token?.Type is JTokenType.String or JTokenType.Null;

        private static bool IsOptionalString(JToken? token) => token == null || IsStringOrNull(token);

        private static bool IsOptionalBoolean(JToken? token) =>
            token == null || token.Type is JTokenType.Boolean or JTokenType.Null;

        private static bool IsOptionalInteger(JToken? token) =>
            token == null || token.Type is JTokenType.Integer or JTokenType.Null;

        private static bool IsOptionalObject(JToken? token) => token == null || token.Type == JTokenType.Null || token is JObject;

        private static bool IsOptionalComposerStash(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return true;
            if (token is not JObject stash
                || !IsOptionalString(stash.GetValue(nameof(CopilotComposerStash.Text), StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var caretIndex = stash.GetValue(nameof(CopilotComposerStash.CaretIndex), StringComparison.OrdinalIgnoreCase);
            var requestMode = stash.GetValue(nameof(CopilotComposerStash.RequestMode), StringComparison.OrdinalIgnoreCase);
            var reviewTarget = stash.GetValue(nameof(CopilotComposerStash.WorkspaceReviewTarget), StringComparison.OrdinalIgnoreCase);
            var skillReference = stash.GetValue(nameof(CopilotComposerStash.AgentSkillReference), StringComparison.OrdinalIgnoreCase);
            var attachments = stash.GetValue(nameof(CopilotComposerStash.Attachments), StringComparison.OrdinalIgnoreCase);
            return (caretIndex == null || caretIndex.Type is JTokenType.Integer or JTokenType.Null)
                && (requestMode == null || requestMode.Type is JTokenType.Integer or JTokenType.Null)
                && IsOptionalReviewTarget(reviewTarget)
                && IsOptionalSkillReference(skillReference)
                && (attachments == null
                    || attachments.Type == JTokenType.Null
                    || attachments is JArray attachmentArray
                        && attachmentArray.All(item => item is JObject));
        }

        private static bool IsOptionalReviewTarget(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return true;
            return token is JObject target
                && IsOptionalInteger(target.GetValue(nameof(CopilotWorkspaceReviewTargetContext.Target), StringComparison.OrdinalIgnoreCase))
                && IsOptionalString(target.GetValue(nameof(CopilotWorkspaceReviewTargetContext.Revision), StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsOptionalSkillReference(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return true;
            return token is JObject reference
                && IsOptionalString(reference.GetValue(nameof(CopilotAgentSkillReference.Name), StringComparison.OrdinalIgnoreCase))
                && IsOptionalString(reference.GetValue(nameof(CopilotAgentSkillReference.SkillFilePath), StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsOptionalPendingSteeringRecoveries(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return true;
            if (token is not JArray records)
                return false;

            return records.All(item => item is JObject record
                && IsOptionalString(record.GetValue(nameof(CopilotPendingSteeringRecoveryRecord.MessageId), StringComparison.OrdinalIgnoreCase))
                && IsOptionalString(record.GetValue(nameof(CopilotPendingSteeringRecoveryRecord.TaskId), StringComparison.OrdinalIgnoreCase))
                && IsOptionalString(record.GetValue(nameof(CopilotPendingSteeringRecoveryRecord.Text), StringComparison.OrdinalIgnoreCase))
                && IsOptionalDate(record.GetValue(nameof(CopilotPendingSteeringRecoveryRecord.AcceptedAtUtc), StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsOptionalDate(JToken? token) =>
            token == null || token.Type is JTokenType.Date or JTokenType.String or JTokenType.Null;
    }
}
