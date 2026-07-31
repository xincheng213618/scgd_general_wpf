using ColorVision.Copilot.Mcp;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

    internal sealed class CopilotTemporaryRedactedOutputArchive : IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly string _path;
        private readonly int _maximumCharacters;
        private FileStream? _stream;
        private long _observedCharacters;
        private int _archivedCharacters;
        private bool _isTruncated;
        private bool _available = true;
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
            if (maximumCharacters <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumCharacters));

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

            var redacted = CopilotMcpAuditLogger.RedactText(
                observed.Replace(
                    "\0",
                    string.Empty,
                    StringComparison.Ordinal));
            lock (_syncRoot)
            {
                _observedCharacters = SaturatingAdd(
                    _observedCharacters,
                    observed.Length);
                if (!_available
                    || _disposed
                    || _stream == null
                    || redacted.Length == 0)
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

                var writeLength = Math.Min(redacted.Length, remaining);
                try
                {
                    _stream.Write(MemoryMarshal.AsBytes(
                        redacted.AsSpan(0, writeLength)));
                    _stream.Flush();
                    _archivedCharacters += writeLength;
                    if (writeLength < redacted.Length)
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
    }
}
