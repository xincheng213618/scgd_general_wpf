using ColorVision.Copilot.Mcp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    internal sealed partial class CopilotTemporaryRedactedOutputArchive : IDisposable
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
                var safeArchiveKind = archiveKind switch
                {
                    "ShellOutput" => "ShellOutput",
                    "ToolOutput" => "ToolOutput",
                    _ => "BackgroundOutput",
                };
                var safeStreamLabel = streamLabel switch
                {
                    "stderr" => "stderr",
                    "content" => "content",
                    _ => "stdout",
                };
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
                    var actualOffsetCharacters = offsetCharacters;
                    if (actualOffsetCharacters > 0
                        && actualOffsetCharacters < _archivedCharacters)
                    {
                        var leadingCharacter = ReadCharacters(
                            stream,
                            1,
                            cancellationToken);
                        if (leadingCharacter.Length == 1
                            && char.IsLowSurrogate(leadingCharacter[0]))
                        {
                            actualOffsetCharacters++;
                        }
                    }
                    stream.Seek(
                        checked((long)actualOffsetCharacters * sizeof(char)),
                        SeekOrigin.Begin);
                    var requestedCharacters = Math.Min(
                        maximumCharacters,
                        _archivedCharacters - actualOffsetCharacters);
                    var content = ReadCharacters(
                        stream,
                        Math.Min(
                            requestedCharacters + 1,
                            _archivedCharacters - actualOffsetCharacters),
                        cancellationToken);
                    content = TakeUnicodeSafePage(
                        content,
                        requestedCharacters);
                    var nextOffset = actualOffsetCharacters + content.Length;
                    return new CopilotRedactedOutputArchivePage(
                        Available: true,
                        Content: content,
                        OffsetCharacters: actualOffsetCharacters,
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
