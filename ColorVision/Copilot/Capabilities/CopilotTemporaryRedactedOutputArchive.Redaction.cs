using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotTemporaryRedactedOutputArchive
    {
        private static string ReadCharacters(
            FileStream stream,
            int count,
            CancellationToken cancellationToken)
        {
            if (count == 0)
                return string.Empty;

            var buffer = new byte[checked(count * sizeof(char))];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(
                    buffer,
                    totalRead,
                    buffer.Length - totalRead);
                if (read == 0)
                    break;

                totalRead += read;
            }

            if (totalRead % sizeof(char) != 0)
            {
                throw new InvalidDataException(
                    "The output archive contains an incomplete character.");
            }

            return new string(
                MemoryMarshal.Cast<byte, char>(
                    buffer.AsSpan(0, totalRead)));
        }

        private void DrainPendingUnderLock(bool flushAll)
        {
            while (_pendingRaw.Length > 0)
            {
                if (_archivedCharacters >= _maximumCharacters)
                {
                    _pendingRaw.Clear();
                    _isTruncated = true;
                    return;
                }

                if (_sensitiveValueTerminator
                    != SensitiveValueTerminator.None)
                {
                    var delimiterIndex = FindSensitiveValueDelimiter(
                        _pendingRaw,
                        _sensitiveValueTerminator);
                    if (delimiterIndex < 0)
                    {
                        _pendingRaw.Clear();
                        return;
                    }

                    var delimiter = _pendingRaw[delimiterIndex];
                    _pendingRaw.Remove(0, delimiterIndex);
                    if (delimiter is '"' or '\'')
                        _pendingRaw.Remove(0, 1);
                    _sensitiveValueTerminator =
                        SensitiveValueTerminator.None;
                    continue;
                }

                var pending = _pendingRaw.ToString();
                var markerIndex = FindSensitiveMarker(
                    pending,
                    out var marker);
                if (markerIndex < 0)
                {
                    var retainedCharacters = flushAll
                        ? 0
                        : Math.Min(
                            MaximumMarkerCharacters - 1,
                            pending.Length);
                    WriteUnderLock(
                        pending.AsSpan(
                            0,
                            pending.Length - retainedCharacters));
                    _pendingRaw.Remove(
                        0,
                        pending.Length - retainedCharacters);
                    return;
                }

                if (markerIndex > 0)
                {
                    WriteUnderLock(pending.AsSpan(0, markerIndex));
                    _pendingRaw.Remove(0, markerIndex);
                    continue;
                }

                if (!TryProcessSensitiveMarkerUnderLock(
                        marker,
                        flushAll))
                {
                    return;
                }
            }
        }

        private bool TryProcessSensitiveMarkerUnderLock(
            string marker,
            bool flushAll)
        {
            var pending = _pendingRaw.ToString();
            var index = marker.Length;
            var hadQuote = false;
            if (index < pending.Length
                && pending[index] is '"' or '\'')
            {
                hadQuote = true;
                index++;
            }

            var whitespaceStart = index;
            while (index < pending.Length
                && char.IsWhiteSpace(pending[index]))
            {
                index++;
            }

            if (index >= pending.Length)
            {
                if (!flushAll)
                    return false;

                WriteUnderLock(pending.AsSpan());
                _pendingRaw.Clear();
                return true;
            }

            if (pending[index] is ':' or '=')
            {
                index++;
                while (index < pending.Length
                    && char.IsWhiteSpace(pending[index]))
                {
                    index++;
                }
                if (index < pending.Length
                    && pending[index] is '"' or '\'')
                {
                    index++;
                }
                if (index >= pending.Length)
                {
                    if (!flushAll)
                        return false;

                    WriteUnderLock(pending.AsSpan());
                    _pendingRaw.Clear();
                    return true;
                }
                if (IsAssignmentValueDelimiter(pending[index]))
                {
                    WriteUnderLock(pending.AsSpan(0, 1));
                    _pendingRaw.Remove(0, 1);
                    return true;
                }

                WriteUnderLock(pending.AsSpan(0, index));
                WriteUnderLock("<redacted>".AsSpan());
                _pendingRaw.Remove(0, index);
                _sensitiveValueTerminator =
                    SensitiveValueTerminator.Assignment;
                return true;
            }

            if (string.Equals(
                    marker,
                    "bearer",
                    StringComparison.OrdinalIgnoreCase)
                && !hadQuote
                && index > whitespaceStart
                && !IsBearerValueDelimiter(pending[index]))
            {
                WriteUnderLock(pending.AsSpan(0, index));
                WriteUnderLock("<redacted>".AsSpan());
                _pendingRaw.Remove(0, index);
                _sensitiveValueTerminator =
                    SensitiveValueTerminator.Bearer;
                return true;
            }

            WriteUnderLock(pending.AsSpan(0, 1));
            _pendingRaw.Remove(0, 1);
            return true;
        }

        private void WriteUnderLock(ReadOnlySpan<char> value)
        {
            if (value.Length == 0
                || !_available
                || _disposed
                || _stream == null)
            {
                return;
            }

            var remaining =
                _maximumCharacters - _archivedCharacters;
            if (remaining <= 0)
            {
                _isTruncated = true;
                return;
            }

            var writeLength = Math.Min(value.Length, remaining);
            try
            {
                _stream.Write(MemoryMarshal.AsBytes(
                    value[..writeLength]));
                _stream.Flush();
                _archivedCharacters += writeLength;
                if (writeLength < value.Length)
                    _isTruncated = true;
            }
            catch (Exception ex) when (
                ex is IOException
                    or ObjectDisposedException
                    or UnauthorizedAccessException
                    or NotSupportedException)
            {
                MarkUnavailableUnderLock(ex);
            }
        }

        private static int FindSensitiveMarker(
            string value,
            out string marker)
        {
            var bestIndex = -1;
            marker = string.Empty;
            foreach (var candidate in SensitiveMarkers)
            {
                var index = value.IndexOf(
                    candidate,
                    StringComparison.OrdinalIgnoreCase);
                if (index < 0
                    || bestIndex >= 0
                        && (index > bestIndex
                            || index == bestIndex
                                && candidate.Length
                                <= marker.Length))
                {
                    continue;
                }

                bestIndex = index;
                marker = candidate;
            }
            return bestIndex;
        }

        private static int FindSensitiveValueDelimiter(
            StringBuilder value,
            SensitiveValueTerminator terminator)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (terminator == SensitiveValueTerminator.Bearer
                    ? IsBearerValueDelimiter(character)
                    : IsAssignmentValueDelimiter(character))
                {
                    return index;
                }
            }
            return -1;
        }

        private static bool IsAssignmentValueDelimiter(char value) =>
            value is ',' or ';' or '\r' or '\n' or '"' or '\'' or '}';

        private static bool IsBearerValueDelimiter(char value) =>
            value is ',' or ';' || char.IsWhiteSpace(value);
    }
}
