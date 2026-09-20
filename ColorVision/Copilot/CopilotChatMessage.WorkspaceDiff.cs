using Newtonsoft.Json;
using System.Linq;

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

        public string WorkspaceDiffWarning
        {
            get => _workspaceDiffWarning;
            set
            {
                if (SetProperty(ref _workspaceDiffWarning, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasWorkspaceDiffWarning));
                    OnPropertyChanged(nameof(WorkspaceDiffHeader));
                }
            }
        }
        private string _workspaceDiffWarning = string.Empty;

        public bool ShouldSerializeWorkspaceDiffWarning() => HasWorkspaceDiffWarning;

        [JsonIgnore]
        public bool HasWorkspaceDiffWarning => !string.IsNullOrWhiteSpace(WorkspaceDiffWarning);

        [JsonIgnore]
        public bool HasWorkspaceDiff => !string.IsNullOrWhiteSpace(WorkspaceDiff);

        [JsonIgnore]
        public string WorkspaceDiffHeader => HasWorkspaceDiff
            ? $"{(HasWorkspaceDiffWarning ? "先前确认的文件变更" : "本轮文件变更")} · {WorkspaceDiffFileCount} 个文件{(IsWorkspaceDiffTruncated ? " · 已截断" : string.Empty)}{(HasWorkspaceDiffWarning ? " · 待核查" : string.Empty)}"
            : string.Empty;

        internal void ApplyWorkspaceDiff(CopilotTurnWorkspaceDiffSnapshot snapshot)
        {
            WorkspaceDiff = snapshot.Diff;
            WorkspaceDiffFileCount = snapshot.FileCount;
            IsWorkspaceDiffTruncated = snapshot.DiffTruncated;
            WorkspaceDiffWarning = snapshot.VerificationWarning;
        }

        private bool RestoreWorkspaceRecheckWarning(bool recoveredWorkspaceWrite)
        {
            if (IsUser || !recoveredWorkspaceWrite && HasWorkspaceDiffWarning) return false;
            var recoveredWarning = CopilotTurnWorkspaceDiffAccumulator.BuildVerificationWarning(AgentTraceEntries
                .Where(entry => entry.Access == CopilotToolAccess.Write
                    && entry.ToolName is "ApplyWorkspacePatchEnvelope" or "RollbackWorkspacePatchEnvelope"
                    && entry.State is CopilotToolExecutionState.Interrupted or CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut)
                .SelectMany(entry => CopilotAgentTraceEntry.CaptureWorkspaceRecheckPaths(entry.WorkspaceRecheckPaths)));
            if (recoveredWarning.Length == 0) return false;
            // Older records can have a warning without structured paths. Preserve it once
            // when newly interrupting a write; repeated validation must not append it again.
            WorkspaceDiffWarning = recoveredWarning + (HasWorkspaceDiffWarning
                ? "\n\n先前待核查记录：\n" + WorkspaceDiffWarning : string.Empty);
            return true;
        }

        private bool EnsureWorkspaceDiffValid()
        {
            var changed = false;
            if (IsUser)
            {
                if (_workspaceDiff.Length > 0 || WorkspaceDiffFileCount != 0 || IsWorkspaceDiffTruncated || _workspaceDiffWarning.Length > 0)
                {
                    WorkspaceDiff = string.Empty;
                    WorkspaceDiffFileCount = 0;
                    IsWorkspaceDiffTruncated = false;
                    WorkspaceDiffWarning = string.Empty;
                    changed = true;
                }
                return changed;
            }

            if (_workspaceDiffWarning.Length > CopilotTurnWorkspaceDiffAccumulator.MaxVerificationWarningCharacters)
            {
                var end = CopilotTurnWorkspaceDiffAccumulator.MaxVerificationWarningCharacters - 1;
                if (char.IsHighSurrogate(_workspaceDiffWarning[end - 1])) end--;
                WorkspaceDiffWarning = _workspaceDiffWarning[..end] + "…";
                changed = true;
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
