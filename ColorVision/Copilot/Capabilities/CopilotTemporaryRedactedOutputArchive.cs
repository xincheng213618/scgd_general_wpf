using ColorVision.Copilot.Mcp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ColorVision.Copilot
{
    internal static class CopilotOutputArchiveLimits
    {
        public const int MaximumArchivedCharacters = 8 * 1024 * 1024;
        public const int DefaultReadCharacters = 8_192;
        public const int MaximumReadCharacters = 16_384;
    }

    internal sealed record CopilotRedactedOutputArchivePage(
        bool Available,
        string Content,
        int OffsetCharacters,
        int ReturnedCharacters,
        int NextOffsetCharacters,
        int ArchivedCharacters,
        bool EndOfAvailableOutput,
        bool ArchiveTruncated,
        string ErrorMessage);

    internal sealed record CopilotRedactedOutputArchiveSearchResult(
        bool Available,
        bool Matched,
        int NextOffsetCharacters,
        int ArchivedCharacters,
        bool ArchiveTruncated,
        string ErrorMessage);

    internal sealed class CopilotTemporaryRedactedOutputArchive : IDisposable
    {
        private static readonly string[] SensitiveMarkers =
        [
            "private_key",
            "private-key",
            "authorization",
            "access_key",
            "access-key",
            "password",
            "api_key",
            "api-key",
            "passwd",
            "apikey",
            "secret",
            "bearer",
            "token",
            "pwd",
        ];
        private static readonly int MaximumMarkerCharacters =
            SensitiveMarkers.Max(marker => marker.Length);
        private readonly object _syncRoot = new();
        private readonly string _path;
        private readonly int _maximumCharacters;
        private readonly StringBuilder _pendingRaw = new();
        private FileStream? _stream;
        private long _observedCharacters;
        private int _archivedCharacters;
        private SensitiveValueTerminator _sensitiveValueTerminator;
        private bool _isTruncated;
        private bool _available = true;
        private bool _completed;
        private bool _disposed;

        private CopilotTemporaryRedactedOutputArchive(
            string path,
            FileStream stream,
            int maximumCharacters)
        {
            _path = path;
            _stream = stream;
            _maximumCharacters = maximumCharacters;
        }

        public bool Available
        {
            get
            {
                lock (_syncRoot)
                    return _available && !_disposed;
            }
        }

        public long ObservedCharacters
        {
            get
            {
                lock (_syncRoot)
                    return _observedCharacters;
            }
        }

        public int ArchivedCharacters
        {
            get
            {
                lock (_syncRoot)
                    return _archivedCharacters;
            }
        }

        public bool IsTruncated
        {
            get
            {
                lock (_syncRoot)
                    return _isTruncated;
            }
        }

        internal string StoragePath => _path;

        public static CopilotTemporaryRedactedOutputArchive? TryCreate(
            string archiveKind,
            string streamLabel,
            int maximumCharacters =
                CopilotOutputArchiveLimits.MaximumArchivedCharacters)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                maximumCharacters);

            try
            {
                var safeArchiveKind =
                    string.Equals(
                        archiveKind,
                        "ShellOutput",
                        StringComparison.Ordinal)
                        ? "ShellOutput"
                        : "BackgroundOutput";
                var safeStreamLabel =
                    string.Equals(
                        streamLabel,
                        "stderr",
                        StringComparison.OrdinalIgnoreCase)
                        ? "stderr"
                        : "stdout";
                var directory = Path.Combine(
                    Path.GetTempPath(),
                    "ColorVision",
                    "Copilot",
                    safeArchiveKind);
                Directory.CreateDirectory(directory);
                var path = Path.Combine(
                    directory,
                    $"{Guid.NewGuid():N}-{safeStreamLabel}.log");
                var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4_096,
                    FileOptions.DeleteOnClose | FileOptions.SequentialScan);
                return new CopilotTemporaryRedactedOutputArchive(
                    path,
                    stream,
                    maximumCharacters);
            }
            catch (Exception ex) when (
                ex is IOException
                    or UnauthorizedAccessException
                    or NotSupportedException)
            {
                Trace.TraceWarning(
                    "Copilot output archive could not be created: "
                    + CopilotMcpAuditLogger.RedactText(ex.Message));
                return null;
            }
        }

        public void Append(string? value)
        {
            var observed = value ?? string.Empty;
            if (observed.Length == 0)
                return;

            lock (_syncRoot)
            {
                _observedCharacters = SaturatingAdd(
                    _observedCharacters,
                    observed.Length);
                if (!_available
                    || _disposed
                    || _stream == null
                    || _completed)
                {
                    return;
                }

                _pendingRaw.Append(
                    observed.Replace(
                        "\0",
                        string.Empty,
                        StringComparison.Ordinal));
                DrainPendingUnderLock(flushAll: false);
            }
        }

        public void Complete()
        {
            lock (_syncRoot)
            {
                if (_completed || _disposed)
                    return;

                DrainPendingUnderLock(flushAll: true);
                _completed = true;
            }
        }

        public CopilotRedactedOutputArchivePage Read(
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                if (!_available || _disposed || _stream == null)
                {
                    return UnavailablePage(
                        offsetCharacters,
                        "The temporary redacted output archive is unavailable.");
                }

                if (offsetCharacters < 0
                    || maximumCharacters <= 0
                    || maximumCharacters
                        > CopilotOutputArchiveLimits.MaximumReadCharacters)
                {
                    return UnavailablePage(
                        offsetCharacters,
                        "The output archive read range is invalid.");
                }

                if (offsetCharacters > _archivedCharacters)
                {
                    return UnavailablePage(
                        offsetCharacters,
                        $"The output archive currently contains {_archivedCharacters} characters; "
                        + $"offset {offsetCharacters} is beyond the available output.");
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _stream.Flush();
                    using var stream = new FileStream(
                        _path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4_096,
                        FileOptions.SequentialScan);
                    stream.Seek(
                        checked((long)offsetCharacters * sizeof(char)),
                        SeekOrigin.Begin);
                    var requestedCharacters = Math.Min(
                        maximumCharacters,
                        _archivedCharacters - offsetCharacters);
                    var content = ReadCharacters(
                        stream,
                        requestedCharacters,
                        cancellationToken);
                    var nextOffset = offsetCharacters + content.Length;
                    return new CopilotRedactedOutputArchivePage(
                        Available: true,
                        Content: content,
                        OffsetCharacters: offsetCharacters,
                        ReturnedCharacters: content.Length,
                        NextOffsetCharacters: nextOffset,
                        ArchivedCharacters: _archivedCharacters,
                        EndOfAvailableOutput:
                            nextOffset >= _archivedCharacters,
                        ArchiveTruncated: _isTruncated,
                        ErrorMessage: string.Empty);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (
                    ex is IOException
                        or ObjectDisposedException
                        or UnauthorizedAccessException
                        or NotSupportedException)
                {
                    MarkUnavailableUnderLock(ex);
                    return UnavailablePage(
                        offsetCharacters,
                        "The temporary redacted output archive could not be read.");
                }
            }
        }

        public CopilotRedactedOutputArchiveSearchResult Search(
            string? literal,
            int offsetCharacters,
            CancellationToken cancellationToken)
        {
            var pattern = literal ?? string.Empty;
            lock (_syncRoot)
            {
                if (!_available || _disposed || _stream == null)
                {
                    return UnavailableSearch(
                        offsetCharacters,
                        "The temporary redacted output archive is unavailable.");
                }

                if (pattern.Length == 0
                    || offsetCharacters < 0
                    || offsetCharacters > _archivedCharacters)
                {
                    return UnavailableSearch(
                        offsetCharacters,
                        "The output archive search range or literal is invalid.");
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _stream.Flush();
                    var archivedCharacters = _archivedCharacters;
                    using var stream = new FileStream(
                        _path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4_096,
                        FileOptions.SequentialScan);
                    stream.Seek(
                        checked((long)offsetCharacters * sizeof(char)),
                        SeekOrigin.Begin);
                    var position = offsetCharacters;
                    var overlapCharacters = Math.Max(0, pattern.Length - 1);
                    var carry = string.Empty;
                    while (position < archivedCharacters)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var requestedCharacters = Math.Min(
                            CopilotOutputArchiveLimits.MaximumReadCharacters,
                            archivedCharacters - position);
                        var content = ReadCharacters(
                            stream,
                            requestedCharacters,
                            cancellationToken);
                        if (content.Length == 0)
                            break;

                        var candidate = carry + content;
                        if (candidate.Contains(
                                pattern,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return new CopilotRedactedOutputArchiveSearchResult(
                                Available: true,
                                Matched: true,
                                NextOffsetCharacters: GetNextSearchOffset(
                                    archivedCharacters,
                                    overlapCharacters,
                                    offsetCharacters),
                                ArchivedCharacters: archivedCharacters,
                                ArchiveTruncated: _isTruncated,
                                ErrorMessage: string.Empty);
                        }

                        carry = overlapCharacters == 0
                            ? string.Empty
                            : candidate[
                                Math.Max(
                                    0,
                                    candidate.Length - overlapCharacters)..];
                        position += content.Length;
                    }

                    return new CopilotRedactedOutputArchiveSearchResult(
                        Available: true,
                        Matched: false,
                        NextOffsetCharacters: GetNextSearchOffset(
                            archivedCharacters,
                            overlapCharacters,
                            offsetCharacters),
                        ArchivedCharacters: archivedCharacters,
                        ArchiveTruncated: _isTruncated,
                        ErrorMessage: string.Empty);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (
                    ex is IOException
                        or ObjectDisposedException
                        or UnauthorizedAccessException
                        or NotSupportedException)
                {
                    MarkUnavailableUnderLock(ex);
                    return UnavailableSearch(
                        offsetCharacters,
                        "The temporary redacted output archive could not be searched.");
                }
            }
        }

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

        private CopilotRedactedOutputArchivePage UnavailablePage(
            int offsetCharacters,
            string errorMessage) =>
            new(
                Available: false,
                Content: string.Empty,
                OffsetCharacters: offsetCharacters,
                ReturnedCharacters: 0,
                NextOffsetCharacters: offsetCharacters,
                ArchivedCharacters: _archivedCharacters,
                EndOfAvailableOutput:
                    offsetCharacters >= _archivedCharacters,
                ArchiveTruncated: _isTruncated,
                ErrorMessage: errorMessage);

        private CopilotRedactedOutputArchiveSearchResult UnavailableSearch(
            int offsetCharacters,
            string errorMessage) =>
            new(
                Available: false,
                Matched: false,
                NextOffsetCharacters: offsetCharacters,
                ArchivedCharacters: _archivedCharacters,
                ArchiveTruncated: _isTruncated,
                ErrorMessage: errorMessage);

        private static int GetNextSearchOffset(
            int archivedCharacters,
            int overlapCharacters,
            int minimumOffset) =>
            Math.Max(
                minimumOffset,
                archivedCharacters
                - Math.Min(archivedCharacters, overlapCharacters));

        private void MarkUnavailableUnderLock(Exception exception)
        {
            _available = false;
            _isTruncated = true;
            Trace.TraceWarning(
                "Copilot output archive became unavailable: "
                + CopilotMcpAuditLogger.RedactText(exception.Message));
            try
            {
                _stream?.Dispose();
            }
            catch (Exception ex) when (
                ex is IOException
                    or ObjectDisposedException)
            {
                Trace.TraceWarning(
                    "Copilot output archive cleanup failed: "
                    + CopilotMcpAuditLogger.RedactText(ex.Message));
            }

            _stream = null;
        }

        private static long SaturatingAdd(long value, int increment) =>
            value > long.MaxValue - increment
                ? long.MaxValue
                : value + increment;

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _available = false;
                try
                {
                    _stream?.Dispose();
                }
                catch (Exception ex) when (
                    ex is IOException
                        or ObjectDisposedException)
                {
                    Trace.TraceWarning(
                        "Copilot output archive cleanup failed: "
                        + CopilotMcpAuditLogger.RedactText(ex.Message));
                }

                _stream = null;
            }
        }

        private enum SensitiveValueTerminator
        {
            None,
            Assignment,
            Bearer,
        }
    }
}
