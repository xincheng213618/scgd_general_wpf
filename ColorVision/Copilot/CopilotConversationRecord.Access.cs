using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotConversationRecord
    {
        [JsonIgnore]
        public CopilotAgentAccessMode AccessMode => _accessContext.Mode;

        [JsonIgnore]
        public bool IsFullAccessPreparedForNextTask => _accessContext.IsPreparedForNextTask;

        [JsonIgnore]
        public string FullAccessTaskId => _accessContext.GrantedTaskId;

        [JsonIgnore]
        public string FullAccessWorkspacePath => _accessContext.WorkspacePath;

        [JsonIgnore]
        public DateTimeOffset? FullAccessExpiresAtUtc => _accessContext.ExpiresAtUtc;

        // AccessMode used to be persisted as an indefinite conversation setting. Read and
        // discard that legacy property so reopening the application always restores the
        // safe per-action confirmation posture.
        [JsonProperty(nameof(AccessMode))]
        private CopilotAgentAccessMode PersistedLegacyAccessMode
        {
            set => _legacyAccessModeLoaded = true;
        }
        private bool _legacyAccessModeLoaded;

        [JsonIgnore]
        internal CopilotAgentAccessContext AccessContext => _accessContext;
        private readonly CopilotAgentAccessContext _accessContext = new();

        internal void PrepareFullAccessGrant(
            string workspacePath,
            string? taskId,
            DateTimeOffset expiresAtUtc)
        {
            _accessContext.PrepareFullAccess(Id, workspacePath, taskId, expiresAtUtc);
            NotifyAccessGrantChanged();
        }

        internal bool BindFullAccessGrantToTask(string taskId, string workspacePath)
        {
            var beforeTaskId = FullAccessTaskId;
            var beforeMode = AccessMode;
            var bound = _accessContext.BindToTask(Id, taskId, workspacePath);
            if (beforeMode != AccessMode
                || !string.Equals(beforeTaskId, FullAccessTaskId, StringComparison.Ordinal))
            {
                NotifyAccessGrantChanged();
            }
            return bound;
        }

        internal bool RevokeFullAccessGrant(string? taskId = null)
        {
            if (!_accessContext.Revoke(taskId))
                return false;

            NotifyAccessGrantChanged();
            return true;
        }

        internal bool ExpireFullAccessGrantIfNeeded()
        {
            if (!_accessContext.ExpireIfNeeded())
                return false;

            NotifyAccessGrantChanged();
            return true;
        }

        private void NotifyAccessGrantChanged()
        {
            OnPropertyChanged(nameof(AccessMode));
            OnPropertyChanged(nameof(IsFullAccessPreparedForNextTask));
            OnPropertyChanged(nameof(FullAccessTaskId));
            OnPropertyChanged(nameof(FullAccessWorkspacePath));
            OnPropertyChanged(nameof(FullAccessExpiresAtUtc));
        }
    }
}
