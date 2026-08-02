using System;
using System.Collections.Generic;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed class CopilotBackgroundShellOutputLineAssembler
    {
        internal const int MaximumLineCharacters = 500;
        internal const int MaximumBatchCharacters = 3_000;
        private const string TruncationSuffix = "...<line truncated>";

        private readonly StringBuilder _pendingLine = new();
        private bool _pendingLineTruncated;

        public IReadOnlyList<string> Append(
            string? content,
            bool flushPartialLine)
        {
            var lines = new List<string>();
            foreach (var character in content ?? string.Empty)
            {
                if (character == '\n')
                {
                    CompleteLine(lines);
                    continue;
                }
                if (character == '\r')
                    continue;
                if (_pendingLine.Length < MaximumLineCharacters)
                    _pendingLine.Append(character);
                else
                    _pendingLineTruncated = true;
            }
            if (flushPartialLine)
                CompleteLine(lines);
            return CreateBatches(lines);
        }

        public IReadOnlyList<string> Flush() =>
            Append(string.Empty, flushPartialLine: true);

        private void CompleteLine(List<string> lines)
        {
            if (_pendingLine.Length == 0 && !_pendingLineTruncated)
                return;

            var line = _pendingLine.ToString();
            if (_pendingLineTruncated)
            {
                var retainedCharacters =
                    MaximumLineCharacters - TruncationSuffix.Length;
                line = line[..Math.Min(line.Length, retainedCharacters)]
                    + TruncationSuffix;
            }
            if (line.Length > 0)
                lines.Add(line);
            _pendingLine.Clear();
            _pendingLineTruncated = false;
        }

        private static IReadOnlyList<string> CreateBatches(
            List<string> lines)
        {
            if (lines.Count == 0)
                return Array.Empty<string>();

            var batches = new List<string>();
            var batch = new StringBuilder();
            foreach (var line in lines)
            {
                var separatorCharacters = batch.Length == 0 ? 0 : 1;
                if (batch.Length > 0
                    && batch.Length + separatorCharacters + line.Length
                        > MaximumBatchCharacters)
                {
                    batches.Add(batch.ToString());
                    batch.Clear();
                }
                if (batch.Length > 0)
                    batch.Append('\n');
                batch.Append(line);
            }
            if (batch.Length > 0)
                batches.Add(batch.ToString());
            return batches;
        }
    }
}
