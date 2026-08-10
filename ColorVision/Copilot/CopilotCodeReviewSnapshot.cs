using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public sealed record CopilotCodeReviewSnapshot(
        string RepositoryRoot,
        string Target,
        string Revision,
        string ResolvedRevision,
        string Scope,
        string PathFilter,
        bool HasChanges,
        bool ToolOutputComplete,
        bool ToolPatchTruncated,
        string ModelObservation)
    {
        internal const int MaximumModelObservationCharacters = 320_000;

        public string EvidenceId { get; init; } = string.Empty;

        public string FindingsResult { get; init; } = string.Empty;

        internal bool IsStructurallyValid()
        {
            return ToolOutputComplete != ToolPatchTruncated
                && CopilotGitDiffResultProtocol.IsMetadataStructurallyValid(
                    RepositoryRoot,
                    Target,
                    Revision,
                    ResolvedRevision,
                    Scope,
                    PathFilter,
                    out _)
                && TryReadModelObservation(out _, out _)
                && IsFindingsStateStructurallyValid();
        }

        internal CopilotCodeReviewSnapshot CreateSnapshot()
        {
            if (!IsStructurallyValid())
                throw new InvalidOperationException("Code review snapshot is invalid.");
            return this with { };
        }

        internal bool TryReadModelObservation(out string content, out bool contentTruncated)
        {
            content = string.Empty;
            contentTruncated = false;
            if (string.IsNullOrWhiteSpace(ModelObservation)
                || ModelObservation.Length > MaximumModelObservationCharacters)
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(
                    ModelObservation,
                    new JsonDocumentOptions { MaxDepth = 8 });
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("tool", out var tool)
                    || tool.ValueKind != JsonValueKind.String
                    || !string.Equals(tool.GetString(), "InspectGitDiff", StringComparison.Ordinal)
                    || !root.TryGetProperty("success", out var success)
                    || success.ValueKind != JsonValueKind.True
                    || !root.TryGetProperty("content", out var modelContent)
                    || modelContent.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                if (root.TryGetProperty("content_truncated", out var truncated))
                {
                    if (truncated.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        return false;
                    contentTruncated = truncated.GetBoolean();
                }

                content = modelContent.GetString() ?? string.Empty;
                return content.Length > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        internal bool TryReadStructuredModelDiff(out CopilotGitDiffSnapshot snapshot)
        {
            snapshot = null!;
            return TryReadModelObservation(out var content, out var contentTruncated)
                && !contentTruncated
                && CopilotGitDiffResultProtocol.TryParse(content, out snapshot, out _);
        }

        internal bool HasModelVisibleGitDiffEvidence()
        {
            if (TryReadStructuredModelDiff(out _))
                return true;
            return TryReadModelObservation(out var content, out var contentTruncated)
                && contentTruncated
                && content.StartsWith(CopilotGitDiffResultProtocol.Header, StringComparison.Ordinal)
                && content.Contains(CopilotGitDiffResultProtocol.ResultJsonMarker, StringComparison.Ordinal);
        }

        internal bool HasFindingsSubmission() =>
            TryReadFindings(out _);

        internal bool TryReadFindings(out IReadOnlyList<CopilotCodeReviewFinding> findings)
        {
            findings = Array.Empty<CopilotCodeReviewFinding>();
            if (!CopilotCodeReviewFindingsResultProtocol.TryParse(
                    FindingsResult,
                    out var submission,
                    out _)
                || !string.Equals(submission.EvidenceId, EvidenceId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            findings = submission.Findings
                .Select(finding => finding with { })
                .ToArray();
            return true;
        }

        internal bool TryApplyFindings(
            string? findingsResult,
            out CopilotCodeReviewSnapshot snapshot)
        {
            snapshot = null!;
            if (!IsStructurallyValid()
                || !CopilotCodeReviewFindingsResultProtocol.TryParse(
                    findingsResult,
                    out var submission,
                    out _)
                || !string.Equals(submission.EvidenceId, EvidenceId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var candidate = this with
            {
                FindingsResult = CopilotCodeReviewFindingsResultProtocol.Serialize(submission),
            };
            if (!candidate.IsStructurallyValid())
                return false;
            snapshot = candidate.CreateSnapshot();
            return true;
        }

        internal static bool TryCreate(
            CopilotGitDiffSnapshot toolSnapshot,
            string? modelObservation,
            out CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(toolSnapshot);
            snapshot = null!;
            if (!toolSnapshot.IsStructurallyValid())
                return false;

            var candidate = new CopilotCodeReviewSnapshot(
                toolSnapshot.RepositoryRoot,
                toolSnapshot.Target,
                toolSnapshot.Revision,
                toolSnapshot.ResolvedRevision,
                toolSnapshot.Scope,
                toolSnapshot.PathFilter,
                toolSnapshot.HasChanges,
                toolSnapshot.OutputComplete,
                toolSnapshot.PatchTruncated,
                modelObservation ?? string.Empty)
            {
                EvidenceId = ComputeEvidenceId(toolSnapshot, modelObservation ?? string.Empty),
            };
            if (!candidate.IsStructurallyValid())
                return false;

            snapshot = candidate.CreateSnapshot();
            return true;
        }

        private bool IsFindingsStateStructurallyValid()
        {
            if (EvidenceId.Length == 0)
                return FindingsResult.Length == 0;
            if (!CopilotCodeReviewFindingsResultProtocol.IsEvidenceIdStructurallyValid(EvidenceId))
                return false;
            return FindingsResult.Length == 0
                || CopilotCodeReviewFindingsResultProtocol.TryParse(
                    FindingsResult,
                    out var submission,
                    out _)
                && string.Equals(submission.EvidenceId, EvidenceId, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeEvidenceId(
            CopilotGitDiffSnapshot toolSnapshot,
            string modelObservation)
        {
            var canonical = CopilotGitDiffResultProtocol.Serialize(toolSnapshot)
                + "\n[Exact Model Observation]\n"
                + modelObservation;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                .ToLowerInvariant();
        }
    }
}
