using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatMessage
    {
        public CopilotCodeReviewSnapshot? CodeReviewSnapshot
        {
            get => _codeReviewSnapshot;
            set
            {
                var normalized = value?.IsStructurallyValid() == true
                    ? value.CreateSnapshot()
                    : value;
                if (Equals(_codeReviewSnapshot, normalized))
                    return;

                _codeReviewSnapshot = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCodeReviewSnapshot));
                OnPropertyChanged(nameof(CodeReviewSnapshotHeader));
            }
        }
        private CopilotCodeReviewSnapshot? _codeReviewSnapshot;

        public bool ShouldSerializeCodeReviewSnapshot() => HasCodeReviewSnapshot;

        [JsonIgnore]
        public bool HasCodeReviewSnapshot => CodeReviewSnapshot?.IsStructurallyValid() == true;

        [JsonIgnore]
        public string CodeReviewSnapshotHeader
        {
            get
            {
                if (!HasCodeReviewSnapshot)
                    return string.Empty;

                var snapshot = CodeReviewSnapshot!;
                var target = snapshot.Target switch
                {
                    "base_branch" => "基线 " + snapshot.Revision,
                    "commit" => "提交 " + ShortenRevision(snapshot.Revision),
                    _ => "当前未提交变更",
                };
                snapshot.TryReadModelObservation(out _, out var modelObservationTruncated);
                var evidenceLimited = snapshot.ToolPatchTruncated
                    || modelObservationTruncated
                    || !snapshot.TryReadStructuredModelDiff(out _);
                var findingsLabel = snapshot.TryReadFindings(out var findings)
                    ? findings.Count == 0
                        ? " · 无 finding"
                        : $" · {findings.Count} 条 finding"
                    : " · Findings 待提交";
                return "代码审查 · " + target
                    + findingsLabel
                    + (evidenceLimited ? " · 证据受限" : string.Empty);
            }
        }

        internal void ApplyCodeReviewSnapshot(CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!snapshot.IsStructurallyValid())
                throw new ArgumentException("Code review snapshot is invalid.", nameof(snapshot));
            CodeReviewSnapshot = snapshot;
        }

        private bool EnsureCodeReviewSnapshotValid()
        {
            if (_codeReviewSnapshot == null)
                return false;
            if (!IsUser
                && RequestMode == CopilotAgentMode.Review
                && _codeReviewSnapshot.IsStructurallyValid())
            {
                return false;
            }

            CodeReviewSnapshot = null;
            return true;
        }

        private static string ShortenRevision(string revision) =>
            revision.Length > 12 ? revision[..12] : revision;
    }
}
