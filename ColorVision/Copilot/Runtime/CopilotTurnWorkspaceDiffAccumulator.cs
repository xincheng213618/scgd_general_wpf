using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotTurnWorkspaceDiffSnapshot(
        string Diff,
        int FileCount,
        bool DiffTruncated)
    {
        public bool IsStructurallyValid()
        {
            if (Diff == null
                || Diff.Length > CopilotTurnWorkspaceDiffAccumulator.MaxDiffCharacters
                || FileCount is < 0 or > CopilotTurnWorkspaceDiffAccumulator.MaxTrackedFiles)
            {
                return false;
            }

            return Diff.Length == 0
                ? FileCount == 0 && !DiffTruncated
                : FileCount > 0;
        }
    }

    internal sealed class CopilotTurnWorkspaceDiffAccumulator
    {
        public const int MaxDiffCharacters = 96_000;
        public const int MaxTrackedFiles = 256;
        public const string DiffTruncationMarker = "...<turn workspace diff truncated>...";
        private const int MaxComparedLinesPerFile = 100_000;
        private const long MaxComparisonCells = 4_000_000;
        private const int ContextLineCount = 3;
        private readonly Dictionary<string, TrackedFile> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _workspacePath;

        public CopilotTurnWorkspaceDiffAccumulator(string? workspacePath)
        {
            _workspacePath = NormalizeWorkspacePath(workspacePath);
        }

        public bool Observe(CopilotAgentEvent agentEvent, out CopilotTurnWorkspaceDiffSnapshot snapshot)
        {
            snapshot = null!;
            var mutation = agentEvent?.Type == CopilotAgentEventType.ToolResult
                && agentEvent.ToolResult?.Success == true
                    ? agentEvent.ToolResult.WorkspaceMutation
                    : null;
            if (mutation == null)
                return false;
            if (!TryApply(mutation))
                throw new InvalidOperationException("Copilot received an invalid or discontinuous workspace mutation snapshot.");

            snapshot = BuildSnapshot();
            return true;
        }

        internal static string BoundPersistedDiff(string? diff, out bool truncated)
        {
            diff ??= string.Empty;
            if (diff.Length <= MaxDiffCharacters)
            {
                truncated = false;
                return diff;
            }

            truncated = true;
            var marker = "\n" + DiffTruncationMarker + "\n";
            var available = MaxDiffCharacters - marker.Length;
            var headLength = available / 2;
            var tailLength = available - headLength;
            headLength = AvoidSplittingSurrogateAtEnd(diff, headLength);
            var tailStart = AvoidSplittingSurrogateAtStart(diff, diff.Length - tailLength);
            return diff[..headLength] + marker + diff[tailStart..];
        }

        private bool TryApply(CopilotWorkspaceMutationSnapshot mutation)
        {
            if (mutation.Files == null || mutation.Files.Count is < 1 or > 8)
                return false;

            var normalized = new List<NormalizedMutation>(mutation.Files.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in mutation.Files)
            {
                if (file == null
                    || string.IsNullOrWhiteSpace(file.FullPath)
                    || file.BeforeText == null
                    || file.AfterText == null)
                {
                    return false;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(file.FullPath);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
                {
                    return false;
                }

                if (!Path.IsPathFullyQualified(fullPath) || !seen.Add(fullPath))
                    return false;
                if (_files.TryGetValue(fullPath, out var current)
                    && (current.CurrentExists != file.BeforeExists
                        || !string.Equals(current.CurrentText, file.BeforeText, StringComparison.Ordinal)))
                {
                    return false;
                }

                normalized.Add(new NormalizedMutation(fullPath, file));
            }

            var nextFiles = new Dictionary<string, TrackedFile>(_files, StringComparer.OrdinalIgnoreCase);
            foreach (var item in normalized)
            {
                var file = item.File;
                var tracked = nextFiles.TryGetValue(item.FullPath, out var existing)
                    ? existing
                    : new TrackedFile(
                        item.FullPath,
                        file.BeforeExists,
                        file.BeforeText,
                        file.BeforeExists,
                        file.BeforeText);
                tracked = tracked with
                {
                    CurrentExists = file.AfterExists,
                    CurrentText = file.AfterText,
                };
                if (tracked.BaselineExists == tracked.CurrentExists
                    && string.Equals(tracked.BaselineText, tracked.CurrentText, StringComparison.Ordinal))
                {
                    nextFiles.Remove(item.FullPath);
                }
                else
                {
                    nextFiles[item.FullPath] = tracked;
                }
            }

            if (nextFiles.Count > MaxTrackedFiles)
                return false;

            _files.Clear();
            foreach (var item in nextFiles)
                _files[item.Key] = item.Value;

            return true;
        }

        private CopilotTurnWorkspaceDiffSnapshot BuildSnapshot()
        {
            if (_files.Count == 0)
                return new CopilotTurnWorkspaceDiffSnapshot(string.Empty, 0, false);

            var builder = new StringBuilder();
            var truncated = false;
            foreach (var file in _files.Values.OrderBy(item => GetDisplayPath(item.FullPath), StringComparer.OrdinalIgnoreCase))
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                AppendFileDiff(builder, file, ref truncated);
            }

            var diff = BoundPersistedDiff(builder.ToString().TrimEnd('\r', '\n'), out var globallyTruncated);
            return new CopilotTurnWorkspaceDiffSnapshot(diff, _files.Count, truncated || globallyTruncated);
        }

        private void AppendFileDiff(StringBuilder builder, TrackedFile file, ref bool truncated)
        {
            var displayPath = GetDisplayPath(file.FullPath);
            builder.Append("--- ").Append(file.BaselineExists ? "a/" + displayPath : "/dev/null").Append('\n');
            builder.Append("+++ ").Append(file.CurrentExists ? "b/" + displayPath : "/dev/null").Append('\n');

            var before = SplitLines(file.BaselineText);
            var after = SplitLines(file.CurrentText);
            if (before.Count > MaxComparedLinesPerFile || after.Count > MaxComparedLinesPerFile)
            {
                builder.Append("...<diff omitted because the file exceeds the comparison line limit>...\n");
                truncated = true;
                return;
            }

            var operations = BuildOperations(before, after);
            AppendHunks(builder, operations);
        }

        private static List<DiffOperation> BuildOperations(
            IReadOnlyList<DiffTextLine> before,
            IReadOnlyList<DiffTextLine> after)
        {
            var prefix = 0;
            while (prefix < before.Count
                && prefix < after.Count
                && before[prefix] == after[prefix])
            {
                prefix++;
            }

            var suffix = 0;
            while (suffix < before.Count - prefix
                && suffix < after.Count - prefix
                && before[before.Count - 1 - suffix] == after[after.Count - 1 - suffix])
            {
                suffix++;
            }

            var operations = new List<DiffOperation>(before.Count + after.Count);
            for (var index = 0; index < prefix; index++)
                operations.Add(new DiffOperation(DiffOperationKind.Equal, before[index]));

            var beforeCount = before.Count - prefix - suffix;
            var afterCount = after.Count - prefix - suffix;
            if (beforeCount == 0)
            {
                for (var index = 0; index < afterCount; index++)
                    operations.Add(new DiffOperation(DiffOperationKind.Added, after[prefix + index]));
            }
            else if (afterCount == 0)
            {
                for (var index = 0; index < beforeCount; index++)
                    operations.Add(new DiffOperation(DiffOperationKind.Removed, before[prefix + index]));
            }
            else if ((long)beforeCount * afterCount <= MaxComparisonCells)
            {
                AppendLcsOperations(operations, before, after, prefix, beforeCount, afterCount);
            }
            else
            {
                for (var index = 0; index < beforeCount; index++)
                    operations.Add(new DiffOperation(DiffOperationKind.Removed, before[prefix + index]));
                for (var index = 0; index < afterCount; index++)
                    operations.Add(new DiffOperation(DiffOperationKind.Added, after[prefix + index]));
            }

            for (var index = suffix; index > 0; index--)
                operations.Add(new DiffOperation(DiffOperationKind.Equal, before[before.Count - index]));
            return operations;
        }

        private static void AppendLcsOperations(
            List<DiffOperation> target,
            IReadOnlyList<DiffTextLine> before,
            IReadOnlyList<DiffTextLine> after,
            int offset,
            int beforeCount,
            int afterCount)
        {
            var directions = new byte[checked(beforeCount * afterCount)];
            var previous = new int[afterCount + 1];
            var current = new int[afterCount + 1];
            for (var oldIndex = 1; oldIndex <= beforeCount; oldIndex++)
            {
                current[0] = 0;
                for (var newIndex = 1; newIndex <= afterCount; newIndex++)
                {
                    var directionIndex = (oldIndex - 1) * afterCount + newIndex - 1;
                    if (before[offset + oldIndex - 1] == after[offset + newIndex - 1])
                    {
                        current[newIndex] = previous[newIndex - 1] + 1;
                        directions[directionIndex] = 1;
                    }
                    else if (previous[newIndex] > current[newIndex - 1])
                    {
                        current[newIndex] = previous[newIndex];
                        directions[directionIndex] = 2;
                    }
                    else
                    {
                        current[newIndex] = current[newIndex - 1];
                        directions[directionIndex] = 3;
                    }
                }

                (previous, current) = (current, previous);
            }

            var reversed = new List<DiffOperation>(beforeCount + afterCount);
            var oldCursor = beforeCount;
            var newCursor = afterCount;
            while (oldCursor > 0 || newCursor > 0)
            {
                if (oldCursor > 0 && newCursor > 0)
                {
                    var direction = directions[(oldCursor - 1) * afterCount + newCursor - 1];
                    if (direction == 1)
                    {
                        reversed.Add(new DiffOperation(DiffOperationKind.Equal, before[offset + oldCursor - 1]));
                        oldCursor--;
                        newCursor--;
                        continue;
                    }
                    if (direction == 2)
                    {
                        reversed.Add(new DiffOperation(DiffOperationKind.Removed, before[offset + oldCursor - 1]));
                        oldCursor--;
                        continue;
                    }
                }

                if (newCursor > 0)
                {
                    reversed.Add(new DiffOperation(DiffOperationKind.Added, after[offset + newCursor - 1]));
                    newCursor--;
                }
                else
                {
                    reversed.Add(new DiffOperation(DiffOperationKind.Removed, before[offset + oldCursor - 1]));
                    oldCursor--;
                }
            }

            for (var index = reversed.Count - 1; index >= 0; index--)
                target.Add(reversed[index]);
        }

        private static void AppendHunks(StringBuilder builder, IReadOnlyList<DiffOperation> operations)
        {
            var changes = operations
                .Select((operation, index) => (operation, index))
                .Where(item => item.operation.Kind != DiffOperationKind.Equal)
                .Select(item => item.index)
                .ToArray();
            if (changes.Length == 0)
                return;

            var oldPositions = new int[operations.Count];
            var newPositions = new int[operations.Count];
            var oldLine = 1;
            var newLine = 1;
            for (var index = 0; index < operations.Count; index++)
            {
                oldPositions[index] = oldLine;
                newPositions[index] = newLine;
                if (operations[index].Kind != DiffOperationKind.Added)
                    oldLine++;
                if (operations[index].Kind != DiffOperationKind.Removed)
                    newLine++;
            }

            var groupStart = Math.Max(0, changes[0] - ContextLineCount);
            var groupEnd = Math.Min(operations.Count, changes[0] + ContextLineCount + 1);
            for (var index = 1; index < changes.Length; index++)
            {
                var nextStart = Math.Max(0, changes[index] - ContextLineCount);
                var nextEnd = Math.Min(operations.Count, changes[index] + ContextLineCount + 1);
                if (nextStart <= groupEnd)
                {
                    groupEnd = Math.Max(groupEnd, nextEnd);
                    continue;
                }

                AppendHunk(builder, operations, oldPositions, newPositions, groupStart, groupEnd);
                groupStart = nextStart;
                groupEnd = nextEnd;
            }

            AppendHunk(builder, operations, oldPositions, newPositions, groupStart, groupEnd);
        }

        private static void AppendHunk(
            StringBuilder builder,
            IReadOnlyList<DiffOperation> operations,
            int[] oldPositions,
            int[] newPositions,
            int start,
            int end)
        {
            var oldCount = 0;
            var newCount = 0;
            for (var index = start; index < end; index++)
            {
                if (operations[index].Kind != DiffOperationKind.Added)
                    oldCount++;
                if (operations[index].Kind != DiffOperationKind.Removed)
                    newCount++;
            }

            var oldStart = oldCount == 0 ? Math.Max(0, oldPositions[start] - 1) : oldPositions[start];
            var newStart = newCount == 0 ? Math.Max(0, newPositions[start] - 1) : newPositions[start];
            builder.Append("@@ -").Append(FormatRange(oldStart, oldCount))
                .Append(" +").Append(FormatRange(newStart, newCount)).Append(" @@\n");
            for (var index = start; index < end; index++)
            {
                var operation = operations[index];
                builder.Append(operation.Kind switch
                {
                    DiffOperationKind.Added => '+',
                    DiffOperationKind.Removed => '-',
                    _ => ' ',
                }).Append(operation.Line.Text).Append('\n');
                if (!operation.Line.HasLineTerminator)
                    builder.Append("\\ No newline at end of file\n");
            }
        }

        private string GetDisplayPath(string fullPath)
        {
            var path = Path.GetFileName(fullPath);
            if (_workspacePath.Length > 0)
            {
                var relative = Path.GetRelativePath(_workspacePath, fullPath);
                if (!relative.Equals("..", StringComparison.Ordinal)
                    && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !Path.IsPathFullyQualified(relative))
                {
                    path = relative;
                }
            }

            return SanitizeDiffPath(path.Replace('\\', '/'));
        }

        private static IReadOnlyList<DiffTextLine> SplitLines(string text)
        {
            if (text.Length == 0)
                return Array.Empty<DiffTextLine>();

            var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var lines = new List<DiffTextLine>();
            var start = 0;
            for (var index = 0; index < normalized.Length; index++)
            {
                if (normalized[index] != '\n')
                    continue;
                lines.Add(new DiffTextLine(normalized[start..index], true));
                start = index + 1;
            }
            if (start < normalized.Length)
                lines.Add(new DiffTextLine(normalized[start..], false));
            return lines;
        }

        private static string FormatRange(int start, int count) => count == 1 ? start.ToString() : $"{start},{count}";

        private static string NormalizeWorkspacePath(string? workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                return string.Empty;
            try
            {
                var fullPath = Path.GetFullPath(workspacePath.Trim());
                return Directory.Exists(fullPath) ? fullPath : string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                return string.Empty;
            }
        }

        private static string SanitizeDiffPath(string path)
        {
            var builder = new StringBuilder(path.Length);
            foreach (var character in path)
                builder.Append(char.IsControl(character) ? '\uFFFD' : character);
            return builder.ToString();
        }

        private static int AvoidSplittingSurrogateAtEnd(string value, int length) =>
            length > 0 && length < value.Length && char.IsHighSurrogate(value[length - 1]) ? length - 1 : length;

        private static int AvoidSplittingSurrogateAtStart(string value, int start) =>
            start > 0 && start < value.Length && char.IsLowSurrogate(value[start]) ? start + 1 : start;

        private sealed record TrackedFile(
            string FullPath,
            bool BaselineExists,
            string BaselineText,
            bool CurrentExists,
            string CurrentText);

        private sealed record NormalizedMutation(string FullPath, CopilotWorkspaceMutationFileSnapshot File);

        private readonly record struct DiffTextLine(string Text, bool HasLineTerminator);

        private readonly record struct DiffOperation(DiffOperationKind Kind, DiffTextLine Line);

        private enum DiffOperationKind
        {
            Equal,
            Added,
            Removed,
        }
    }
}
