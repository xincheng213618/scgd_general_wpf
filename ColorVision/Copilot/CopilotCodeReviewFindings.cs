using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    public sealed record CopilotCodeReviewFinding(
        string Priority,
        string Title,
        string Body,
        string Path,
        string Side,
        int LineStart,
        int LineEnd);

    internal sealed record CopilotCodeReviewFindingsSubmission(
        string EvidenceId,
        IReadOnlyList<CopilotCodeReviewFinding> Findings);

    internal static class CopilotCodeReviewFindingsResultProtocol
    {
        internal const string Header = "[Code Review Findings]";
        internal const string ResultJsonMarker = "result_json: ";
        internal const int MaximumFindings = 50;
        internal const int MaximumTitleCharacters = 240;
        internal const int MaximumBodyCharacters = 4_000;
        internal const int MaximumSerializedCharacters = 128_000;
        private const int MaximumLineNumber = 10_000_000;
        private const int MaximumLineSpan = 500;

        public static string Serialize(CopilotCodeReviewFindingsSubmission submission)
        {
            ArgumentNullException.ThrowIfNull(submission);
            if (!IsStructurallyValid(submission))
                throw new ArgumentException("Code review findings submission is invalid.", nameof(submission));

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["evidence_id"] = submission.EvidenceId,
                ["findings"] = submission.Findings.Select(finding =>
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["priority"] = finding.Priority,
                        ["title"] = finding.Title,
                        ["body"] = finding.Body,
                        ["path"] = finding.Path,
                        ["side"] = finding.Side,
                        ["line_start"] = finding.LineStart,
                        ["line_end"] = finding.LineEnd,
                    }).ToArray(),
            };
            return $"{Header}\n{ResultJsonMarker}{JsonSerializer.Serialize(payload)}";
        }

        public static bool TryParse(
            string? content,
            out CopilotCodeReviewFindingsSubmission submission,
            out string error)
        {
            submission = EmptySubmission();
            error = string.Empty;
            var normalized = content ?? string.Empty;
            if (!normalized.StartsWith(Header + "\n", StringComparison.Ordinal))
            {
                error = "Code review findings result has no protocol header.";
                return false;
            }

            var markerIndex = normalized.IndexOf(ResultJsonMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                error = "Code review findings result has no structured payload.";
                return false;
            }

            var json = normalized[(markerIndex + ResultJsonMarker.Length)..];
            if (json.Length == 0 || json.Length > MaximumSerializedCharacters)
            {
                error = "Code review findings result exceeds the structured payload limit.";
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !HasOnlyProperties(root, "evidence_id", "findings")
                    || !TryReadString(root, "evidence_id", out var evidenceId)
                    || !root.TryGetProperty("findings", out var findingsElement)
                    || findingsElement.ValueKind != JsonValueKind.Array
                    || findingsElement.GetArrayLength() > MaximumFindings)
                {
                    error = "Code review findings result does not match the expected schema.";
                    return false;
                }

                var findings = new List<CopilotCodeReviewFinding>(findingsElement.GetArrayLength());
                foreach (var item in findingsElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || !HasOnlyProperties(
                            item,
                            "priority",
                            "title",
                            "body",
                            "path",
                            "side",
                            "line_start",
                            "line_end")
                        || !TryReadString(item, "priority", out var priority)
                        || !TryReadString(item, "title", out var title)
                        || !TryReadString(item, "body", out var body)
                        || !TryReadString(item, "path", out var path)
                        || !TryReadString(item, "side", out var side)
                        || !TryReadInt32(item, "line_start", out var lineStart)
                        || !TryReadInt32(item, "line_end", out var lineEnd))
                    {
                        error = "Code review findings result contains an invalid finding.";
                        return false;
                    }

                    findings.Add(new CopilotCodeReviewFinding(
                        priority,
                        title,
                        body,
                        path,
                        side,
                        lineStart,
                        lineEnd));
                }

                var parsed = new CopilotCodeReviewFindingsSubmission(
                    evidenceId.ToLowerInvariant(),
                    findings.ToArray());
                if (!IsStructurallyValid(parsed))
                {
                    error = "Code review findings result contains inconsistent or out-of-bounds data.";
                    return false;
                }

                submission = CreateSnapshot(parsed);
                return true;
            }
            catch (JsonException ex)
            {
                error = "Code review findings result is not valid JSON: " + ex.Message;
                return false;
            }
        }

        public static bool TryNormalizeFinding(
            CopilotCodeReviewFinding finding,
            out CopilotCodeReviewFinding normalized,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(finding);
            error = string.Empty;
            var priority = (finding.Priority ?? string.Empty).Trim().ToUpperInvariant();
            var title = NormalizeText(finding.Title, preserveLines: false);
            var body = NormalizeText(finding.Body, preserveLines: true);
            var path = (finding.Path ?? string.Empty).Trim().Replace('\\', '/');
            var side = (finding.Side ?? string.Empty).Trim().ToLowerInvariant();
            normalized = new CopilotCodeReviewFinding(
                priority,
                title,
                body,
                path,
                side,
                finding.LineStart,
                finding.LineEnd);
            if (!IsFindingStructurallyValid(normalized))
            {
                error = "Each finding needs P0-P3 priority, bounded title/body text, a repository-relative path, new/old side, and a valid bounded line range.";
                return false;
            }

            return true;
        }

        public static bool IsStructurallyValid(CopilotCodeReviewFindingsSubmission? submission)
        {
            return submission != null
                && IsEvidenceIdStructurallyValid(submission.EvidenceId)
                && submission.Findings != null
                && submission.Findings.Count <= MaximumFindings
                && submission.Findings.All(IsFindingStructurallyValid)
                && submission.Findings.SequenceEqual(OrderFindings(submission.Findings));
        }

        public static bool IsEvidenceIdStructurallyValid(string? evidenceId) =>
            evidenceId?.Length == 64 && evidenceId.All(Uri.IsHexDigit);

        public static IReadOnlyList<CopilotCodeReviewFinding> OrderFindings(
            IEnumerable<CopilotCodeReviewFinding> findings) =>
            findings
                .OrderBy(finding => finding.Priority, StringComparer.Ordinal)
                .ThenBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(finding => finding.Side, StringComparer.Ordinal)
                .ThenBy(finding => finding.LineStart)
                .ThenBy(finding => finding.LineEnd)
                .ThenBy(finding => finding.Title, StringComparer.Ordinal)
                .ToArray();

        public static CopilotCodeReviewFindingsSubmission CreateSnapshot(
            CopilotCodeReviewFindingsSubmission submission)
        {
            ArgumentNullException.ThrowIfNull(submission);
            if (!IsStructurallyValid(submission))
                throw new ArgumentException("Code review findings submission is invalid.", nameof(submission));
            return submission with { Findings = submission.Findings.Select(finding => finding with { }).ToArray() };
        }

        private static bool IsFindingStructurallyValid(CopilotCodeReviewFinding? finding)
        {
            return finding != null
                && finding.Priority is "P0" or "P1" or "P2" or "P3"
                && !string.IsNullOrWhiteSpace(finding.Title)
                && finding.Title.Length <= MaximumTitleCharacters
                && !finding.Title.Any(char.IsControl)
                && !string.IsNullOrWhiteSpace(finding.Body)
                && finding.Body.Length <= MaximumBodyCharacters
                && !finding.Body.Contains('\0')
                && CopilotGitDiffResultProtocol.IsChangedPathStructurallyValid(finding.Path)
                && finding.Side is "new" or "old"
                && finding.LineStart is >= 1 and <= MaximumLineNumber
                && finding.LineEnd >= finding.LineStart
                && finding.LineEnd <= MaximumLineNumber
                && finding.LineEnd - finding.LineStart <= MaximumLineSpan;
        }

        private static string NormalizeText(string? value, bool preserveLines)
        {
            var text = CopilotMcpAuditLogger.RedactText(value ?? string.Empty)
                .Replace("\0", string.Empty, StringComparison.Ordinal)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim();
            if (!preserveLines)
                text = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text;
        }

        private static bool HasOnlyProperties(JsonElement element, params string[] names)
        {
            var expected = names.ToHashSet(StringComparer.Ordinal);
            var actualCount = 0;
            foreach (var property in element.EnumerateObject())
            {
                actualCount++;
                if (!expected.Contains(property.Name))
                    return false;
            }
            return actualCount == expected.Count;
        }

        private static bool TryReadString(JsonElement element, string name, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(name, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            value = property.GetString() ?? string.Empty;
            return true;
        }

        private static bool TryReadInt32(JsonElement element, string name, out int value)
        {
            value = 0;
            return element.TryGetProperty(name, out var property)
                && property.ValueKind == JsonValueKind.Number
                && property.TryGetInt32(out value);
        }

        private static CopilotCodeReviewFindingsSubmission EmptySubmission() =>
            new(string.Empty, Array.Empty<CopilotCodeReviewFinding>());
    }

    internal static class CopilotGitDiffLineCoverage
    {
        private static readonly Regex HunkHeader = new(
            @"^@@ -(?<oldStart>\d+)(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        public static bool Contains(
            CopilotGitDiffSnapshot diff,
            CopilotCodeReviewFinding finding)
        {
            ArgumentNullException.ThrowIfNull(diff);
            ArgumentNullException.ThrowIfNull(finding);
            if (!diff.IsStructurallyValid()
                || !diff.ChangedPaths.Contains(finding.Path, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var section in diff.Sections)
            {
                string oldPath = string.Empty;
                string newPath = string.Empty;
                var inHunk = false;
                long oldLine = 0, newLine = 0, oldRemaining = 0, newRemaining = 0;
                var visibleRangeStarted = false;
                var lines = section.Patch.Split('\n');
                for (var index = 0; index < lines.Length; index++)
                {
                    var normalizedLine = lines[index].TrimEnd('\r');
                    // Truncators can insert a newline after a partial prefix row.
                    // Neither that row nor the unlocated tail has reliable coverage.
                    if (IsTruncationMarker(normalizedLine, section)
                        || index + 1 < lines.Length && IsTruncationMarker(lines[index + 1].TrimEnd('\r'), section)
                        || index == lines.Length - 1 && section.PatchTruncated)
                    {
                        inHunk = false;
                        visibleRangeStarted = false;
                        continue;
                    }
                    if (normalizedLine.StartsWith("diff --git ", StringComparison.Ordinal))
                    {
                        oldPath = newPath = string.Empty;
                        inHunk = false;
                    }
                    if (!inHunk && normalizedLine.StartsWith("--- ", StringComparison.Ordinal))
                    {
                        oldPath = ParsePatchPath(normalizedLine[4..], "a/");
                        continue;
                    }
                    if (!inHunk && normalizedLine.StartsWith("+++ ", StringComparison.Ordinal))
                    {
                        newPath = ParsePatchPath(normalizedLine[4..], "b/");
                        continue;
                    }
                    if (normalizedLine.StartsWith("@@ ", StringComparison.Ordinal))
                    {
                        var match = HunkHeader.Match(normalizedLine);
                        inHunk = match.Success
                            && TryReadHunkNumber(match, "oldStart", out oldLine)
                            && TryReadHunkNumber(match, "newStart", out newLine)
                            && TryReadHunkNumber(match, "oldCount", out oldRemaining)
                            && TryReadHunkNumber(match, "newCount", out newRemaining);
                        visibleRangeStarted = false;
                        continue;
                    }
                    if (!inHunk || normalizedLine.Length == 0 || normalizedLine[0] == '\\')
                        continue;
                    var consumesOld = normalizedLine[0] is ' ' or '-';
                    var consumesNew = normalizedLine[0] is ' ' or '+';
                    if (!consumesOld && !consumesNew
                        || consumesOld && oldRemaining == 0
                        || consumesNew && newRemaining == 0)
                    {
                        inHunk = false;
                        continue;
                    }
                    var sidePath = finding.Side == "old" ? oldPath : newPath;
                    var sideLine = finding.Side == "old" ? oldLine : newLine;
                    if ((finding.Side == "old" ? consumesOld : consumesNew)
                        && string.Equals(sidePath, finding.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        visibleRangeStarted |= sideLine == finding.LineStart;
                        if (visibleRangeStarted && sideLine == finding.LineEnd)
                            return true;
                    }
                    if (consumesOld) { oldLine++; oldRemaining--; }
                    if (consumesNew) { newLine++; newRemaining--; }
                    inHunk = oldRemaining > 0 || newRemaining > 0;
                }
            }

            return false;
        }

        private static string ParsePatchPath(string value, string prefix)
        {
            var path = value.Split('\t', 2)[0].Trim();
            if (path.StartsWith('"'))
            {
                if (!TryDecodeQuotedPath(path, out path))
                    return string.Empty;
            }
            if (string.Equals(path, "/dev/null", StringComparison.Ordinal))
                return string.Empty;
            return path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : string.Empty;
        }

        private static bool TryReadHunkNumber(Match match, string name, out long value)
        {
            value = 1;
            return !match.Groups[name].Success
                || long.TryParse(match.Groups[name].Value, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsTruncationMarker(string line, CopilotGitDiffSection section) =>
            line is "...<Git diff excerpt truncated>..." or "...<shell output truncated>..."
            || section.Scope == "untracked" && section.PatchTruncated
                && line is "+...<untracked file content truncated>..." or "+...<untracked file became unavailable during review>...";

        private static bool TryDecodeQuotedPath(string value, out string path)
        {
            path = string.Empty;
            if (value.Length < 2 || value[^1] != '"')
                return false;
            var builder = new StringBuilder();
            for (var index = 1; index < value.Length - 1; index++)
            {
                var character = value[index];
                if (character != '\\') { builder.Append(character); continue; }
                if (++index >= value.Length - 1)
                    return false;
                character = value[index];
                if (character is >= '0' and <= '7')
                {
                    var bytes = new List<byte>();
                    do
                    {
                        var octal = character - '0';
                        for (var digit = 1; digit < 3 && index + 1 < value.Length - 1
                            && value[index + 1] is >= '0' and <= '7'; digit++)
                            octal = octal * 8 + value[++index] - '0';
                        if (octal > byte.MaxValue)
                            return false;
                        bytes.Add((byte)octal);
                        if (index + 2 >= value.Length - 1 || value[index + 1] != '\\'
                            || value[index + 2] is < '0' or > '7')
                            break;
                        index += 2;
                        character = value[index];
                    } while (true);
                    try { builder.Append(new UTF8Encoding(false, true).GetString(bytes.ToArray())); }
                    catch (DecoderFallbackException) { return false; }
                    continue;
                }
                var decoded = character switch
                {
                    'a' => '\a', 'b' => '\b', 't' => '\t', 'n' => '\n',
                    'v' => '\v', 'f' => '\f', 'r' => '\r', '\\' => '\\', '"' => '"', _ => '\0',
                };
                if (decoded == '\0')
                    return false;
                builder.Append(decoded);
            }
            path = builder.ToString();
            return true;
        }
    }

    internal sealed class CopilotReviewEvidenceContext
    {
        private readonly object _sync = new();
        private CopilotCodeReviewSnapshot? _latestEvidence;

        public void InvalidateEvidence()
        {
            lock (_sync)
                _latestEvidence = null;
        }

        public void RecordEvidence(CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!snapshot.IsStructurallyValid()
                || !CopilotCodeReviewFindingsResultProtocol.IsEvidenceIdStructurallyValid(snapshot.EvidenceId))
            {
                throw new ArgumentException("Code review evidence snapshot is invalid.", nameof(snapshot));
            }

            lock (_sync)
                _latestEvidence = snapshot.CreateSnapshot();
        }

        public bool TryCreateSubmission(
            IReadOnlyList<CopilotCodeReviewFinding> findings,
            out string content,
            out string error)
        {
            content = string.Empty;
            error = string.Empty;
            findings ??= Array.Empty<CopilotCodeReviewFinding>();
            if (findings.Count > CopilotCodeReviewFindingsResultProtocol.MaximumFindings)
            {
                error = $"At most {CopilotCodeReviewFindingsResultProtocol.MaximumFindings} findings may be submitted.";
                return false;
            }

            CopilotCodeReviewSnapshot? evidence;
            lock (_sync)
                evidence = _latestEvidence?.CreateSnapshot();
            if (evidence == null)
            {
                error = "Call InspectGitDiff successfully before submitting code review findings.";
                return false;
            }

            var normalized = new List<CopilotCodeReviewFinding>(findings.Count);
            foreach (var finding in findings)
            {
                if (!CopilotCodeReviewFindingsResultProtocol.TryNormalizeFinding(
                        finding,
                        out var normalizedFinding,
                        out error))
                {
                    return false;
                }
                normalized.Add(normalizedFinding);
            }

            var ordered = CopilotCodeReviewFindingsResultProtocol.OrderFindings(normalized);
            if (ordered.Count > 0)
            {
                if (!evidence.TryReadStructuredModelDiff(out var modelDiff))
                {
                    error = "Non-empty findings require a complete structured Git diff in the exact model-visible evidence. Submit an empty findings array and disclose the evidence limit instead of inventing an unseen line.";
                    return false;
                }

                var ungrounded = ordered.FirstOrDefault(finding =>
                    !CopilotGitDiffLineCoverage.Contains(modelDiff, finding));
                if (ungrounded != null)
                {
                    error = $"Finding location {ungrounded.Path}:{ungrounded.LineStart} ({ungrounded.Side}) is not inside a visible Git diff hunk.";
                    return false;
                }
            }

            var submission = new CopilotCodeReviewFindingsSubmission(
                evidence.EvidenceId,
                ordered);
            content = CopilotCodeReviewFindingsResultProtocol.Serialize(submission);
            return true;
        }
    }
}
