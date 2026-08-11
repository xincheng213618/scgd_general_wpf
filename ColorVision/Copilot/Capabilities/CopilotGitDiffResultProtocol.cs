using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal static class CopilotGitDiffResultProtocol
    {
        internal const string Header = "[Git Diff Inspection]";
        internal const string ResultJsonMarker = "result_json: ";
        internal const int MaxSerializedCharacters = 320_000;
        internal const int MaxChangedPaths = 1_024;
        private const int MaxPathCharacters = 32_768;
        private const int MaxChangedPathCharacters = 2_048;

        public static string Serialize(CopilotGitDiffSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!IsStructurallyValid(snapshot))
                throw new ArgumentException("Git diff snapshot is invalid.", nameof(snapshot));

            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["repository_root"] = snapshot.RepositoryRoot,
                ["target"] = snapshot.Target,
                ["revision"] = snapshot.Revision,
                ["resolved_revision"] = snapshot.ResolvedRevision,
                ["scope"] = snapshot.Scope,
                ["path_filter"] = snapshot.PathFilter,
                ["changed_paths"] = snapshot.ChangedPaths,
                ["changed_paths_complete"] = snapshot.ChangedPathsComplete,
                ["has_changes"] = snapshot.HasChanges,
                ["output_complete"] = snapshot.OutputComplete,
                ["patch_truncated"] = snapshot.PatchTruncated,
                ["sections"] = snapshot.Sections.Select(section => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["scope"] = section.Scope,
                    ["has_changes"] = section.HasChanges,
                    ["output_complete"] = section.OutputComplete,
                    ["patch_truncated"] = section.PatchTruncated,
                    ["patch"] = section.Patch,
                }).ToArray(),
            };
            return $"{Header}\n{ResultJsonMarker}{JsonSerializer.Serialize(result)}";
        }

        public static bool TryParse(
            string? content,
            out CopilotGitDiffSnapshot snapshot,
            out string error)
        {
            snapshot = EmptySnapshot();
            error = string.Empty;
            var normalizedContent = content ?? string.Empty;
            var markerIndex = normalizedContent.IndexOf(ResultJsonMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                error = "Git diff result has no structured payload.";
                return false;
            }

            var json = normalizedContent[(markerIndex + ResultJsonMarker.Length)..];
            if (json.Length == 0 || json.Length > MaxSerializedCharacters)
            {
                error = "Git diff result exceeds the structured payload limit.";
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !TryReadString(root, "repository_root", out var repositoryRoot)
                    || !TryReadString(root, "target", out var target)
                    || !TryReadString(root, "revision", out var revision)
                    || !TryReadString(root, "resolved_revision", out var resolvedRevision)
                    || !TryReadString(root, "scope", out var scope)
                    || !TryReadString(root, "path_filter", out var pathFilter)
                    || !TryReadBoolean(root, "has_changes", out var hasChanges)
                    || !TryReadBoolean(root, "output_complete", out var outputComplete)
                    || !TryReadBoolean(root, "patch_truncated", out var patchTruncated)
                    || !root.TryGetProperty("sections", out var sectionsElement)
                    || sectionsElement.ValueKind != JsonValueKind.Array)
                {
                    error = "Git diff result does not match the expected schema.";
                    return false;
                }

                var changedPaths = Array.Empty<string>();
                var changedPathsComplete = false;
                var hasChangedPathMetadata = root.TryGetProperty("changed_paths", out _)
                    || root.TryGetProperty("changed_paths_complete", out _);
                if (hasChangedPathMetadata)
                {
                    if (!TryReadStringArray(root, "changed_paths", out changedPaths)
                        || !TryReadBoolean(root, "changed_paths_complete", out changedPathsComplete))
                    {
                        error = "Git diff result contains invalid changed-path metadata.";
                        return false;
                    }
                }

                var sections = new List<CopilotGitDiffSection>();
                foreach (var sectionElement in sectionsElement.EnumerateArray())
                {
                    if (sectionElement.ValueKind != JsonValueKind.Object
                        || !TryReadString(sectionElement, "scope", out var sectionScope)
                        || !TryReadBoolean(sectionElement, "has_changes", out var sectionHasChanges)
                        || !TryReadBoolean(sectionElement, "output_complete", out var sectionOutputComplete)
                        || !TryReadBoolean(sectionElement, "patch_truncated", out var sectionPatchTruncated)
                        || !TryReadString(sectionElement, "patch", out var patch))
                    {
                        error = "Git diff result contains an invalid section.";
                        return false;
                    }

                    sections.Add(new CopilotGitDiffSection(
                        sectionScope,
                        sectionHasChanges,
                        sectionOutputComplete,
                        sectionPatchTruncated,
                        patch));
                }
                NormalizeLegacyWorkingTreeSections(
                    target,
                    scope,
                    hasChangedPathMetadata,
                    sections);

                var parsed = new CopilotGitDiffSnapshot(
                    repositoryRoot,
                    scope,
                    pathFilter,
                    hasChanges,
                    outputComplete,
                    patchTruncated,
                    sections)
                {
                    Target = target,
                    Revision = revision,
                    ResolvedRevision = resolvedRevision,
                    ChangedPaths = changedPaths,
                    ChangedPathsComplete = changedPathsComplete,
                };
                if (!IsStructurallyValid(parsed))
                {
                    error = "Git diff result contains inconsistent or out-of-bounds data.";
                    return false;
                }

                snapshot = CreateSnapshot(parsed);
                return true;
            }
            catch (JsonException ex)
            {
                error = "Git diff result is not valid JSON: " + ex.Message;
                return false;
            }
        }

        public static bool IsStructurallyValid(CopilotGitDiffSnapshot? snapshot)
        {
            if (snapshot == null
                || !IsMetadataStructurallyValid(
                    snapshot.RepositoryRoot,
                    snapshot.Target,
                    snapshot.Revision,
                    snapshot.ResolvedRevision,
                    snapshot.Scope,
                    snapshot.PathFilter,
                    out var expectedSectionScopes)
                || snapshot.Sections == null
                || snapshot.ChangedPaths == null
                || snapshot.ChangedPaths.Count > MaxChangedPaths
                || snapshot.ChangedPaths.Any(path => !IsChangedPathStructurallyValid(path))
                || snapshot.ChangedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    != snapshot.ChangedPaths.Count
                || snapshot.Sections.Count is < 1 or > 3)
            {
                return false;
            }

            if (snapshot.Sections.Count != expectedSectionScopes.Count)
                return false;

            for (var index = 0; index < snapshot.Sections.Count; index++)
            {
                var section = snapshot.Sections[index];
                if (section == null
                    || !string.Equals(section.Scope, expectedSectionScopes[index], StringComparison.Ordinal)
                    || section.Patch == null
                    || section.Patch.Length > CopilotGitDiffInspectionService.MaxPatchCharactersPerSection
                    || section.HasChanges != !string.IsNullOrWhiteSpace(section.Patch)
                    || section.OutputComplete == section.PatchTruncated)
                {
                    return false;
                }
            }

            return snapshot.HasChanges == snapshot.Sections.Any(section => section.HasChanges)
                && snapshot.OutputComplete == snapshot.Sections.All(section => section.OutputComplete)
                && snapshot.PatchTruncated == snapshot.Sections.Any(section => section.PatchTruncated)
                && snapshot.OutputComplete != snapshot.PatchTruncated;
        }

        internal static bool IsMetadataStructurallyValid(
            string? repositoryRoot,
            string? target,
            string? revision,
            string? resolvedRevision,
            string? scope,
            string? pathFilter,
            out IReadOnlyList<string> expectedSectionScopes)
        {
            expectedSectionScopes = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(repositoryRoot)
                || repositoryRoot.Length > MaxPathCharacters
                || ContainsControlCharacter(repositoryRoot)
                || scope == null
                || pathFilter == null
                || pathFilter.Length > MaxPathCharacters
                || ContainsControlCharacter(pathFilter)
                || target == null
                || revision == null
                || resolvedRevision == null)
            {
                return false;
            }

            if (string.Equals(target, "working_tree", StringComparison.Ordinal))
            {
                if (scope is not ("unstaged" or "staged" or "both")
                    || revision.Length != 0
                    || resolvedRevision.Length != 0)
                {
                    return false;
                }

                expectedSectionScopes = scope switch
                {
                    "both" => ["unstaged", "staged", "untracked"],
                    "staged" => ["staged"],
                    _ => ["unstaged", "untracked"],
                };
                return true;
            }

            if (target is not ("base_branch" or "commit")
                || !string.Equals(scope, "unstaged", StringComparison.Ordinal)
                || !CopilotGitDiffInspectionService.TryValidateRevision(target, revision, out _)
                || !IsObjectId(resolvedRevision))
            {
                return false;
            }

            expectedSectionScopes = [target];
            return true;
        }

        public static CopilotGitDiffSnapshot CreateSnapshot(CopilotGitDiffSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!IsStructurallyValid(snapshot))
                throw new ArgumentException("Git diff snapshot is invalid.", nameof(snapshot));
            return snapshot with
            {
                Sections = snapshot.Sections
                    .Select(section => section with { })
                    .ToArray(),
                ChangedPaths = snapshot.ChangedPaths.ToArray(),
            };
        }

        public static bool AreEquivalent(
            CopilotGitDiffSnapshot? left,
            CopilotGitDiffSnapshot? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null
                || left.Sections == null
                || right.Sections == null
                || !string.Equals(left.RepositoryRoot, right.RepositoryRoot, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(left.Target, right.Target, StringComparison.Ordinal)
                || !string.Equals(left.Revision, right.Revision, StringComparison.Ordinal)
                || !string.Equals(left.ResolvedRevision, right.ResolvedRevision, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(left.Scope, right.Scope, StringComparison.Ordinal)
                || !string.Equals(left.PathFilter, right.PathFilter, StringComparison.Ordinal)
                || left.ChangedPaths == null
                || right.ChangedPaths == null
                || left.ChangedPathsComplete != right.ChangedPathsComplete
                || !left.ChangedPaths.SequenceEqual(right.ChangedPaths, StringComparer.OrdinalIgnoreCase)
                || left.HasChanges != right.HasChanges
                || left.OutputComplete != right.OutputComplete
                || left.PatchTruncated != right.PatchTruncated
                || left.Sections.Count != right.Sections.Count)
            {
                return false;
            }

            return left.Sections
                .Zip(right.Sections)
                .All(pair => pair.First == pair.Second);
        }

        private static bool TryReadString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString() ?? string.Empty;
            return true;
        }

        private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
        {
            value = false;
            if (!element.TryGetProperty(propertyName, out var property)
                || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            value = property.GetBoolean();
            return true;
        }

        private static bool TryReadStringArray(
            JsonElement element,
            string propertyName,
            out string[] values)
        {
            values = Array.Empty<string>();
            if (!element.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.Array
                || property.GetArrayLength() > MaxChangedPaths)
            {
                return false;
            }

            var parsed = new List<string>(property.GetArrayLength());
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    return false;
                parsed.Add(item.GetString() ?? string.Empty);
            }
            values = parsed.ToArray();
            return true;
        }

        private static void NormalizeLegacyWorkingTreeSections(
            string target,
            string scope,
            bool hasChangedPathMetadata,
            List<CopilotGitDiffSection> sections)
        {
            if (hasChangedPathMetadata
                || !string.Equals(target, "working_tree", StringComparison.Ordinal)
                || scope is not ("unstaged" or "both")
                || sections.Any(section => string.Equals(
                    section.Scope,
                    "untracked",
                    StringComparison.Ordinal)))
            {
                return;
            }

            var expectedLegacyScopes = scope == "both"
                ? new[] { "unstaged", "staged" }
                : ["unstaged"];
            if (!sections.Select(section => section.Scope).SequenceEqual(
                expectedLegacyScopes,
                StringComparer.Ordinal))
            {
                return;
            }

            sections.Add(new CopilotGitDiffSection(
                "untracked",
                false,
                true,
                false,
                string.Empty));
        }

        internal static bool IsChangedPathStructurallyValid(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.Length > MaxChangedPathCharacters
                || ContainsControlCharacter(path)
                || Path.IsPathRooted(path)
                || path.Contains('\\'))
            {
                return false;
            }

            var segments = path.Split('/', StringSplitOptions.None);
            return segments.Length > 0
                && segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
        }

        private static bool ContainsControlCharacter(string value) =>
            value.Any(char.IsControl);

        private static bool IsObjectId(string value) =>
            value.Length is 40 or 64 && value.All(Uri.IsHexDigit);

        private static CopilotGitDiffSnapshot EmptySnapshot() => new(
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            false,
            false,
            Array.Empty<CopilotGitDiffSection>());
    }
}
