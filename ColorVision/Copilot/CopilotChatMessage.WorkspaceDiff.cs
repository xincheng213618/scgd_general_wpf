using Newtonsoft.Json;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatMessage
    {
        public string WorkspaceDiff
        {
            get => _workspaceDiff;
            set
            {
                if (SetProperty(ref _workspaceDiff, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasWorkspaceDiff));
                    OnPropertyChanged(nameof(WorkspaceDiffHeader));
                }
            }
        }
        private string _workspaceDiff = string.Empty;

        public bool ShouldSerializeWorkspaceDiff() => HasWorkspaceDiff;

        public int WorkspaceDiffFileCount
        {
            get => _workspaceDiffFileCount;
            set
            {
                var normalized = System.Math.Clamp(value, 0, CopilotTurnWorkspaceDiffAccumulator.MaxTrackedFiles);
                if (SetProperty(ref _workspaceDiffFileCount, normalized))
                    OnPropertyChanged(nameof(WorkspaceDiffHeader));
            }
        }
        private int _workspaceDiffFileCount;

        public bool ShouldSerializeWorkspaceDiffFileCount() => HasWorkspaceDiff;

        public bool IsWorkspaceDiffTruncated
        {
            get => _isWorkspaceDiffTruncated;
            set
            {
                if (SetProperty(ref _isWorkspaceDiffTruncated, value))
                    OnPropertyChanged(nameof(WorkspaceDiffHeader));
            }
        }
        private bool _isWorkspaceDiffTruncated;

        public bool ShouldSerializeIsWorkspaceDiffTruncated() => HasWorkspaceDiff && IsWorkspaceDiffTruncated;

        [JsonIgnore]
        public bool HasWorkspaceDiff => !string.IsNullOrWhiteSpace(WorkspaceDiff);

        [JsonIgnore]
        public string WorkspaceDiffHeader => HasWorkspaceDiff
            ? $"本轮文件变更 · {WorkspaceDiffFileCount} 个文件{(IsWorkspaceDiffTruncated ? " · 已截断" : string.Empty)}"
            : string.Empty;

        internal void ApplyWorkspaceDiff(CopilotTurnWorkspaceDiffSnapshot snapshot)
        {
            WorkspaceDiff = snapshot.Diff;
            WorkspaceDiffFileCount = snapshot.FileCount;
            IsWorkspaceDiffTruncated = snapshot.DiffTruncated;
        }

        private bool EnsureWorkspaceDiffValid()
        {
            var changed = false;
            if (IsUser)
            {
                if (_workspaceDiff.Length > 0 || WorkspaceDiffFileCount != 0 || IsWorkspaceDiffTruncated)
                {
                    WorkspaceDiff = string.Empty;
                    WorkspaceDiffFileCount = 0;
                    IsWorkspaceDiffTruncated = false;
                    changed = true;
                }
                return changed;
            }

            var boundedDiff = CopilotTurnWorkspaceDiffAccumulator.BoundPersistedDiff(_workspaceDiff, out var bounded);
            if (!string.Equals(_workspaceDiff, boundedDiff, System.StringComparison.Ordinal))
            {
                WorkspaceDiff = boundedDiff;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(_workspaceDiff))
            {
                if (_workspaceDiff.Length > 0)
                {
                    WorkspaceDiff = string.Empty;
                    changed = true;
                }
                if (WorkspaceDiffFileCount != 0)
                {
                    WorkspaceDiffFileCount = 0;
                    changed = true;
                }
                if (IsWorkspaceDiffTruncated)
                {
                    IsWorkspaceDiffTruncated = false;
                    changed = true;
                }
                return changed;
            }

            if (WorkspaceDiffFileCount == 0)
            {
                WorkspaceDiffFileCount = 1;
                changed = true;
            }
            if (bounded && !IsWorkspaceDiffTruncated)
            {
                IsWorkspaceDiffTruncated = true;
                changed = true;
            }
            return changed;
        }
    }
}
