#pragma warning disable CA1001
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotChatStateLoadSource
    {
        NotAttempted,
        Fresh,
        Primary,
        Temporary,
        Backup,
        RecoverySnapshot,
        FutureVersion,
        Unrecoverable,
    }

    public readonly record struct CopilotChatStateLoadStatus(CopilotChatStateLoadSource Source, int? SchemaVersion = null)
    {
        public bool IsRecovery => Source is CopilotChatStateLoadSource.Temporary
            or CopilotChatStateLoadSource.Backup
            or CopilotChatStateLoadSource.RecoverySnapshot;

        public bool IsUnrecoverable => Source == CopilotChatStateLoadSource.Unrecoverable;

        public bool IsFutureVersion => Source == CopilotChatStateLoadSource.FutureVersion;

        public bool RequiresRecoveryProtection => IsUnrecoverable || IsFutureVersion;
    }

    public sealed class CopilotChatStateSizeLimitException : IOException
    {
        public long ActualBytes { get; }

        public long MaximumBytes { get; }

        public CopilotChatStateSizeLimitException(long actualBytes, long maximumBytes)
            : base($"Copilot state snapshot exceeded the size limit ({actualBytes / 1024d / 1024d:F1} MB of {maximumBytes / 1024 / 1024} MB).")
        {
            ActualBytes = actualBytes;
            MaximumBytes = maximumBytes;
        }
    }

    public sealed class CopilotChatStateFutureVersionException : IOException
    {
        public int SchemaVersion { get; }

        public int SupportedSchemaVersion { get; }

        public CopilotChatStateFutureVersionException(int schemaVersion, int supportedSchemaVersion)
            : base($"Copilot state schema {schemaVersion} was created by a newer application version; this version supports schema {supportedSchemaVersion}.")
        {
            SchemaVersion = schemaVersion;
            SupportedSchemaVersion = supportedSchemaVersion;
        }
    }

    public sealed class CopilotChatStateSnapshot
    {
        internal JObject Document { get; }

        internal CopilotChatStateSnapshot(JObject document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }
    }

    public sealed class CopilotChatStateSnapshotCapture
    {
        private readonly JsonSerializer _serializer;
        private readonly JObject _document;
        private readonly JArray _conversationDocuments = new();
        private readonly JArray? _queuedFollowUpRecoveryDocuments;
        private readonly CopilotConversationRecord[] _conversations;
        private readonly CopilotQueuedFollowUpRecoveryRecord[] _queuedFollowUpRecoveries;
        private int _conversationIndex;
        private int _queuedFollowUpRecoveryIndex;

        internal CopilotChatStateSnapshotCapture(CopilotChatState state, JsonSerializerSettings serializerSettings)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(serializerSettings);

            state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
            _serializer = JsonSerializer.Create(serializerSettings);
            _conversations = state.Conversations?.ToArray() ?? [];
            _queuedFollowUpRecoveries = state.QueuedFollowUpRecoveries?.ToArray() ?? [];
            _queuedFollowUpRecoveryDocuments = _queuedFollowUpRecoveries.Length > 0 ? new JArray() : null;
            _document = new JObject
            {
                [nameof(CopilotChatState.SchemaVersion)] = state.SchemaVersion,
                [nameof(CopilotChatState.Conversations)] = _conversationDocuments,
            };
            AddStringProperty(_document, nameof(CopilotChatState.ActiveConversationId), state.ActiveConversationId);
            AddStringProperty(_document, nameof(CopilotChatState.ActiveProfileId), state.ActiveProfileId);
            if (!state.IsAgentTaskPanelExpanded)
                _document[nameof(CopilotChatState.IsAgentTaskPanelExpanded)] = false;
            if (!state.ShowMessageTimestamps)
                _document[nameof(CopilotChatState.ShowMessageTimestamps)] = false;
            if (state.UseCompactMessageLayout)
                _document[nameof(CopilotChatState.UseCompactMessageLayout)] = true;
            if (!state.EnablePromptHistoryCompletions)
                _document[nameof(CopilotChatState.EnablePromptHistoryCompletions)] = false;
            if (state.UseMultilineComposer)
                _document[nameof(CopilotChatState.UseMultilineComposer)] = true;
            var followUpBehavior = CopilotFollowUpPreference.Normalize(state.DefaultFollowUpBehavior);
            if (followUpBehavior != CopilotFollowUpBehavior.Steer)
            {
                _document[nameof(CopilotChatState.DefaultFollowUpBehavior)] =
                    (int)followUpBehavior;
            }
            if (_queuedFollowUpRecoveryDocuments != null)
                _document[nameof(CopilotChatState.QueuedFollowUpRecoveries)] = _queuedFollowUpRecoveryDocuments;
        }

        public bool IsComplete =>
            _conversationIndex >= _conversations.Length
            && _queuedFollowUpRecoveryIndex >= _queuedFollowUpRecoveries.Length;

        public bool CaptureNextChunk()
        {
            if (_conversationIndex < _conversations.Length)
            {
                AddObject(_conversationDocuments, _conversations[_conversationIndex++]);
                return true;
            }

            if (_queuedFollowUpRecoveryIndex < _queuedFollowUpRecoveries.Length)
            {
                AddObject(_queuedFollowUpRecoveryDocuments!, _queuedFollowUpRecoveries[_queuedFollowUpRecoveryIndex++]);
                return true;
            }

            return false;
        }

        public CopilotChatStateSnapshot Complete()
        {
            if (!IsComplete)
                throw new InvalidOperationException("Copilot state snapshot capture is incomplete.");

            return new CopilotChatStateSnapshot(_document);
        }

        private void AddObject(JArray target, object? value)
        {
            if (value == null)
            {
                target.Add(JValue.CreateNull());
                return;
            }

            var builder = new StringBuilder();
            using var stringWriter = new StringWriter(builder, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            _serializer.Serialize(jsonWriter, value);
            jsonWriter.Flush();
            target.Add(new JRaw(builder.ToString()));
        }

        private static void AddStringProperty(JObject document, string propertyName, string? value)
        {
            if (value != null)
                document[propertyName] = value;
        }
    }

    public interface ICopilotChatStateStore
    {
        string AttachmentDirectoryPath { get; }

        CopilotChatState Load();

        void Save(CopilotChatState state);

        CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state);

        string Serialize(CopilotChatStateSnapshot snapshot);

        string Serialize(CopilotChatState state);

        Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default);

        int CleanupOrphanedAttachments(CopilotChatState state);
    }

    public interface IIncrementalCopilotChatStateStore : ICopilotChatStateStore
    {
        CopilotChatStateSnapshotCapture BeginSnapshot(CopilotChatState state);
    }

}
