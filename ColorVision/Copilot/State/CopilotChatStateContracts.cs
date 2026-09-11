#pragma warning disable CA1001
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
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

    public readonly record struct CopilotChatStateLoadStatus(
        CopilotChatStateLoadSource Source,
        int? SchemaVersion = null,
        string StateFilePath = "")
    {
        public bool IsRecovery => Source is CopilotChatStateLoadSource.Temporary
            or CopilotChatStateLoadSource.Backup
            or CopilotChatStateLoadSource.RecoverySnapshot;

        public bool IsUnrecoverable => Source == CopilotChatStateLoadSource.Unrecoverable;

        public bool IsFutureVersion => Source == CopilotChatStateLoadSource.FutureVersion;

        public bool RequiresRecoveryProtection => IsUnrecoverable || IsFutureVersion
            || Source is CopilotChatStateLoadSource.Backup or CopilotChatStateLoadSource.RecoverySnapshot;
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

        public string StateFilePath { get; }

        public CopilotChatStateFutureVersionException(int schemaVersion, int supportedSchemaVersion)
            : this(schemaVersion, supportedSchemaVersion, string.Empty)
        {
        }

        public CopilotChatStateFutureVersionException(
            int schemaVersion,
            int supportedSchemaVersion,
            string? stateFilePath)
            : base(CreateMessage(schemaVersion, supportedSchemaVersion, stateFilePath))
        {
            SchemaVersion = schemaVersion;
            SupportedSchemaVersion = supportedSchemaVersion;
            StateFilePath = string.IsNullOrWhiteSpace(stateFilePath)
                ? string.Empty
                : Path.GetFullPath(stateFilePath);
        }

        private static string CreateMessage(
            int schemaVersion,
            int supportedSchemaVersion,
            string? stateFilePath)
        {
            var location = string.IsNullOrWhiteSpace(stateFilePath)
                ? string.Empty
                : $" The protected state file is '{Path.GetFullPath(stateFilePath)}'.";
            return $"Copilot state schema {schemaVersion} was created by a newer application version; this version supports schema {supportedSchemaVersion}.{location}";
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
        private readonly JsonSerializer _incrementalSerializer;
        private readonly StringBuilder _messageBuffer = new();
        private readonly JObject _document;
        private readonly JArray _conversationDocuments = new();
        private readonly JArray? _queuedFollowUpRecoveryDocuments;
        private readonly CopilotConversationRecord[] _conversations;
        private readonly CopilotQueuedFollowUpRecoveryRecord[] _queuedFollowUpRecoveries;
        private readonly Queue<PendingCollectionCapture> _pendingCollections = new();
        private int _conversationIndex;
        private int _queuedFollowUpRecoveryIndex;

        internal CopilotChatStateSnapshotCapture(CopilotChatState state, JsonSerializerSettings serializerSettings)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(serializerSettings);

            state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
            _serializer = JsonSerializer.Create(serializerSettings);
            _incrementalSerializer = JsonSerializer.Create(serializerSettings);
            _incrementalSerializer.Converters.Add(new DeferredCollectionConverter(
                _incrementalSerializer.ContractResolver,
                _pendingCollections));
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
            && _queuedFollowUpRecoveryIndex >= _queuedFollowUpRecoveries.Length
            && _pendingCollections.Count == 0;

        public bool CaptureNextChunk()
        {
            if (_pendingCollections.TryPeek(out var collection))
            {
                AddObject(collection.Document, collection.Items[collection.Index++]);
                if (collection.Index >= collection.Items.Length)
                    _pendingCollections.Dequeue();
                return true;
            }

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

            if (value is not CopilotChatMessage)
            {
                // Keep Json.NET's property names, converters, private persisted properties and
                // ShouldSerialize rules. Only collection contents are deferred to later chunks.
                target.Add(JToken.FromObject(value, _incrementalSerializer));
                return;
            }

            // A message, including its trace/timeline and compressed request text, remains one
            // atomic unit. JRaw avoids building another large token tree for each message.
            _messageBuffer.Clear();
            using var stringWriter = new StringWriter(_messageBuffer, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            _serializer.Serialize(jsonWriter, value);
            jsonWriter.Flush();
            target.Add(new JRaw(_messageBuffer.ToString()));
        }

        private sealed class PendingCollectionCapture(JArray document, object?[] items)
        {
            public JArray Document { get; } = document;
            public object?[] Items { get; } = items;
            public int Index { get; set; }
        }

        private sealed class DeferredCollectionConverter(
            IContractResolver contractResolver,
            Queue<PendingCollectionCapture> pendingCollections) : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType) =>
                contractResolver.ResolveContract(objectType) is JsonArrayContract;

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteStartArray();
                var document = (JArray)((JTokenWriter)writer).CurrentToken!;
                // Capture collection membership while on the owning dispatcher; later edits
                // cannot invalidate an enumerator or change the started collection's membership.
                var items = ((IEnumerable)value).Cast<object?>().ToArray();
                if (items.Length > 0)
                    pendingCollections.Enqueue(new PendingCollectionCapture(document, items));
                writer.WriteEndArray();
            }

            public override object? ReadJson(
                JsonReader reader,
                Type objectType,
                object? existingValue,
                JsonSerializer serializer) => throw new NotSupportedException();
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
