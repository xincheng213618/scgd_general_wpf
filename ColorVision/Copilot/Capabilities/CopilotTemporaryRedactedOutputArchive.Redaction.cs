using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotTemporaryRedactedOutputArchive
    {
        private char _sensitiveValueQuote;
        private bool _sensitiveValueEscapePending;
        private bool _openAiCredentialContinuation;
        private int _hiddenPendingCharacters;
        private bool _hasPreviousRawCharacter;
        private char _previousRawCharacter;
        private bool _linePrefixHasOnlyHorizontalWhitespace = true;
        private static readonly Regex AwsAccessKeyIdRegex = new(
            @"\bAKIA[0-9A-Z]{16}\b",
            RegexOptions.Compiled);
        private static readonly Regex AuthorizationWordBoundaryRegex = new(
            @"\bauthorization$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                if (_archivedCharacters >= _maximumCharacters
                    && _hiddenPendingCharacters == 0
                    && !_openAiCredentialContinuation
                    && _sensitiveValueTerminator == SensitiveValueTerminator.None)
                {
                    ConsumePendingPrefix(_pendingRaw.Length);
                    _isTruncated = true;
                    return;
                }

                if (_openAiCredentialContinuation)
                {
                    var end = 0;
                    while (end < _pendingRaw.Length
                        && IsAsciiLetterOrDigit(_pendingRaw[end]))
                    {
                        end++;
                    }
                    var reachesPendingEnd = end == _pendingRaw.Length;
                    RetainMaskedCredentialTail(end);
                    if (reachesPendingEnd && !flushAll)
                        return;
                    _openAiCredentialContinuation = false;
                }

                if (_sensitiveValueTerminator
                    != SensitiveValueTerminator.None)
                {
                    var delimiterIndex = FindSensitiveValueDelimiter(
                        _pendingRaw,
                        _sensitiveValueTerminator);
                    if (delimiterIndex < 0)
                    {
                        ConsumePendingPrefix(_pendingRaw.Length);
                        return;
                    }

                    var delimiter = _pendingRaw[delimiterIndex];
                    if (_sensitiveValueTerminator == SensitiveValueTerminator.AuthorizationInline
                        && _sensitiveValueQuote != '\0'
                        && delimiter == _sensitiveValueQuote)
                    {
                        ConsumePendingPrefix(delimiterIndex + 1);
                        _sensitiveValueQuote = '\0';
                        _sensitiveValueEscapePending = false;
                        continue;
                    }
                    ConsumePendingPrefix(delimiterIndex);
                    if (_sensitiveValueTerminator == SensitiveValueTerminator.Assignment
                        && (delimiter is '"' or '\''))
                    {
                        if (_sensitiveValueQuote != '\0')
                            WritePendingPrefix(_pendingRaw.ToString(0, 1), 1);
                        ConsumePendingPrefix(1);
                        _sensitiveValueTerminator = SensitiveValueTerminator.AssignmentRemainder;
                        _sensitiveValueQuote = '\0';
                        _sensitiveValueEscapePending = false;
                        continue;
                    }
                    if (_sensitiveValueQuote == '\0' && (delimiter is '"' or '\''))
                        ConsumePendingPrefix(1);
                    _sensitiveValueTerminator =
                        SensitiveValueTerminator.None;
                    _sensitiveValueQuote = '\0';
                    _sensitiveValueEscapePending = false;
                    continue;
                }

                var pending = _pendingRaw.ToString();
                var markerIndex = FindRedactionCandidate(
                    pending,
                    out var marker,
                    out var candidateKind);
                if (markerIndex < 0)
                {
                    var retainedCharacters = flushAll
                        ? 0
                        : Math.Min(
                            MaximumMarkerCharacters - 1,
                            pending.Length);
                    var writeLength = GetUnicodeSafePrefixLength(
                        pending,
                        pending.Length - retainedCharacters);
                    WritePendingPrefix(pending, writeLength);
                    ConsumePendingPrefix(writeLength);
                    return;
                }

                if (markerIndex > 0)
                {
                    WritePendingPrefix(pending, markerIndex);
                    ConsumePendingPrefix(markerIndex);
                    continue;
                }

                var processed = candidateKind == RedactionCandidateKind.Assignment
                    ? TryProcessSensitiveMarkerUnderLock(marker, flushAll)
                    : TryProcessStandaloneCredentialUnderLock(candidateKind, flushAll);
                if (!processed)
                {
                    return;
                }
            }
        }

        private bool TryProcessStandaloneCredentialUnderLock(
            RedactionCandidateKind candidateKind,
            bool flushAll)
        {
            var pending = _pendingRaw.ToString();
            if (candidateKind == RedactionCandidateKind.OpenAiCredential)
            {
                const int prefixCharacters = 3;
                const int minimumCredentialCharacters = 20;
                var end = prefixCharacters;
                while (end < pending.Length && IsAsciiLetterOrDigit(pending[end]))
                    end++;
                if (end - prefixCharacters < minimumCredentialCharacters)
                {
                    if (end == pending.Length && !flushAll)
                        return false;
                    ReleaseRejectedCandidateCharacter();
                    return true;
                }

                WriteUnderLock("<redacted>".AsSpan());
                _openAiCredentialContinuation = end == pending.Length && !flushAll;
                RetainMaskedCredentialTail(end);
                return true;
            }

            const int awsCredentialCharacters = 20;
            var credentialEnd = 4;
            while (credentialEnd < pending.Length
                && credentialEnd < awsCredentialCharacters
                && IsAsciiUpperLetterOrDigit(pending[credentialEnd]))
            {
                credentialEnd++;
            }
            if (credentialEnd < awsCredentialCharacters)
            {
                if (credentialEnd == pending.Length && !flushAll)
                    return false;
                ReleaseRejectedCandidateCharacter();
                return true;
            }
            if (pending.Length == awsCredentialCharacters && !flushAll)
                return false;

            // A bounded context window delegates Unicode word-boundary semantics
            // to the same .NET regex used by whole-text credential redaction.
            var candidate = pending[..Math.Min(pending.Length, awsCredentialCharacters + 1)];
            var expectedIndex = _hasPreviousRawCharacter ? 1 : 0;
            var window = _hasPreviousRawCharacter
                ? _previousRawCharacter + candidate
                : candidate;
            var match = AwsAccessKeyIdRegex.Match(window);
            if (!match.Success
                || match.Index != expectedIndex
                || match.Length != awsCredentialCharacters)
            {
                ReleaseRejectedCandidateCharacter();
                return true;
            }

            WriteUnderLock("<redacted>".AsSpan());
            RetainMaskedCredentialTail(awsCredentialCharacters);
            return true;
        }

        private void RetainMaskedCredentialTail(int count)
        {
            // A credential can end in all or part of an assignment name. Keep
            // that bounded raw suffix for the existing parser, but never emit it.
            var retained = Math.Min(count, MaximumMarkerCharacters);
            ConsumePendingPrefix(count - retained);
            _hiddenPendingCharacters = Math.Max(_hiddenPendingCharacters, retained);
        }

        private void WritePendingPrefix(string pending, int count)
        {
            var hidden = Math.Min(count, _hiddenPendingCharacters);
            WriteUnderLock(pending.AsSpan(hidden, count - hidden));
        }

        private void ReleaseRejectedCandidateCharacter()
        {
            // Re-enter marker scanning rather than writing an entire rejected
            // candidate that could contain an ordinary sensitive assignment.
            WritePendingPrefix(_pendingRaw.ToString(0, 1), 1);
            ConsumePendingPrefix(1);
        }

        private void ConsumePendingPrefix(int count)
        {
            if (count == 0)
                return;
            for (var index = 0; index < count; index++)
            {
                var character = _pendingRaw[index];
                if (character == '\n')
                    _linePrefixHasOnlyHorizontalWhitespace = true;
                else if (character is not (' ' or '\t'))
                    _linePrefixHasOnlyHorizontalWhitespace = false;
            }
            _previousRawCharacter = _pendingRaw[count - 1];
            _hasPreviousRawCharacter = true;
            _pendingRaw.Remove(0, count);
            _hiddenPendingCharacters = Math.Max(0, _hiddenPendingCharacters - count);
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

                WritePendingPrefix(pending, pending.Length);
                ConsumePendingPrefix(_pendingRaw.Length);
                return true;
            }

            if (pending[index] is ':' or '=')
            {
                var authorizationTerminator = GetAuthorizationHeaderTerminator(marker, pending, index, hadQuote);
                if (authorizationTerminator != SensitiveValueTerminator.None)
                {
                    var valueStart = index + 1;
                    while (valueStart < pending.Length
                        && (authorizationTerminator == SensitiveValueTerminator.AuthorizationLine
                            ? pending[valueStart] is ' ' or '\t'
                            : char.IsWhiteSpace(pending[valueStart])))
                    {
                        valueStart++;
                    }
                    if (valueStart == pending.Length && !flushAll)
                        return false;

                    WritePendingPrefix(pending, valueStart);
                    WriteUnderLock("<redacted>".AsSpan());
                    _sensitiveValueQuote = authorizationTerminator == SensitiveValueTerminator.AuthorizationInline
                        && valueStart < pending.Length && (pending[valueStart] is '"' or '\'')
                            ? pending[valueStart++]
                            : '\0';
                    ConsumePendingPrefix(valueStart);
                    _sensitiveValueTerminator = authorizationTerminator;
                    _sensitiveValueEscapePending = false;
                    return true;
                }

                var valueQuote = '\0';
                index++;
                while (index < pending.Length
                    && char.IsWhiteSpace(pending[index]))
                {
                    index++;
                }
                if (index < pending.Length
                    && pending[index] is '"' or '\'')
                {
                    valueQuote = pending[index];
                    index++;
                }
                if (index >= pending.Length)
                {
                    if (!flushAll)
                        return false;

                    WritePendingPrefix(pending, pending.Length);
                    ConsumePendingPrefix(_pendingRaw.Length);
                    return true;
                }
                if (valueQuote != '\0' && pending[index] == valueQuote)
                {
                    WritePendingPrefix(pending, index + 1);
                    ConsumePendingPrefix(index + 1);
                    return true;
                }
                if (valueQuote == '\0' && IsAssignmentValueDelimiter(pending[index]))
                {
                    WritePendingPrefix(pending, 1);
                    ConsumePendingPrefix(1);
                    return true;
                }

                WritePendingPrefix(pending, index);
                WriteUnderLock("<redacted>".AsSpan());
                ConsumePendingPrefix(index);
                _sensitiveValueTerminator =
                    SensitiveValueTerminator.Assignment;
                _sensitiveValueQuote = valueQuote;
                _sensitiveValueEscapePending = false;
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
                WritePendingPrefix(pending, index);
                WriteUnderLock("<redacted>".AsSpan());
                ConsumePendingPrefix(index);
                _sensitiveValueTerminator =
                    SensitiveValueTerminator.Bearer;
                return true;
            }

            WritePendingPrefix(pending, 1);
            ConsumePendingPrefix(1);
            return true;
        }

        private SensitiveValueTerminator GetAuthorizationHeaderTerminator(
            string marker, string pending, int separatorIndex, bool hadQuote)
        {
            var proxyHeader = string.Equals(marker, "proxy-authorization", StringComparison.OrdinalIgnoreCase);
            if (hadQuote || !proxyHeader && !string.Equals(marker, "authorization", StringComparison.OrdinalIgnoreCase))
                return SensitiveValueTerminator.None;

            var horizontalWhitespace = true;
            for (var index = marker.Length; index < separatorIndex; index++)
                horizontalWhitespace &= pending[index] is ' ' or '\t';
            if (_linePrefixHasOnlyHorizontalWhitespace && horizontalWhitespace && pending[separatorIndex] == ':')
                return SensitiveValueTerminator.AuthorizationLine;

            var boundaryContext = _hasPreviousRawCharacter ? _previousRawCharacter + "authorization" : "authorization";
            return proxyHeader || AuthorizationWordBoundaryRegex.IsMatch(boundaryContext)
                ? SensitiveValueTerminator.AuthorizationInline
                : SensitiveValueTerminator.None;
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
            if (writeLength < value.Length
                && writeLength > 0
                && char.IsHighSurrogate(value[writeLength - 1])
                && char.IsLowSurrogate(value[writeLength]))
            {
                writeLength--;
            }
            try
            {
                if (writeLength > 0)
                {
                    _stream.Write(MemoryMarshal.AsBytes(
                        value[..writeLength]));
                }
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

        private static int GetUnicodeSafePrefixLength(
            string value,
            int maximumCharacters)
        {
            var length = Math.Clamp(
                maximumCharacters,
                0,
                value.Length);
            if (length > 0
                && length < value.Length
                && char.IsHighSurrogate(value[length - 1])
                && char.IsLowSurrogate(value[length]))
            {
                length--;
            }
            return length;
        }

        private static string TakeUnicodeSafePage(
            string value,
            int maximumCharacters)
        {
            if (value.Length <= maximumCharacters)
            {
                return value.Length > 0
                    && char.IsHighSurrogate(value[^1])
                    ? value[..^1]
                    : value;
            }

            var length = maximumCharacters;
            if (length > 0
                && char.IsHighSurrogate(value[length - 1])
                && char.IsLowSurrogate(value[length]))
            {
                length++;
            }
            return value[..length];
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

        private static int FindRedactionCandidate(
            string value,
            out string marker,
            out RedactionCandidateKind candidateKind)
        {
            var index = FindSensitiveMarker(value, out marker);
            candidateKind = RedactionCandidateKind.Assignment;
            var openAiIndex = value.IndexOf("sk-", StringComparison.Ordinal);
            if (openAiIndex >= 0 && (index < 0 || openAiIndex < index))
            {
                index = openAiIndex;
                candidateKind = RedactionCandidateKind.OpenAiCredential;
            }
            var awsIndex = value.IndexOf("AKIA", StringComparison.Ordinal);
            if (awsIndex >= 0 && (index < 0 || awsIndex < index))
            {
                index = awsIndex;
                candidateKind = RedactionCandidateKind.AwsCredential;
            }
            return index;
        }

        private static bool IsAsciiLetterOrDigit(char value) =>
            value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

        private static bool IsAsciiUpperLetterOrDigit(char value) =>
            value is >= 'A' and <= 'Z' or >= '0' and <= '9';

        private enum RedactionCandidateKind
        {
            Assignment,
            OpenAiCredential,
            AwsCredential,
        }

        private int FindSensitiveValueDelimiter(
            StringBuilder value,
            SensitiveValueTerminator terminator)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((terminator is SensitiveValueTerminator.AuthorizationLine or SensitiveValueTerminator.AuthorizationInline)
                    && (character is '\r' or '\n'))
                {
                    return index;
                }
                if (terminator == SensitiveValueTerminator.AuthorizationLine)
                    continue;
                if (terminator == SensitiveValueTerminator.AssignmentRemainder)
                {
                    if (character is ',' or ';' or '}' || char.IsWhiteSpace(character))
                        return index;
                    continue;
                }
                if (terminator == SensitiveValueTerminator.AuthorizationInline && _sensitiveValueQuote == '\0')
                {
                    if (character == ';')
                        return index;
                    continue;
                }
                if (_sensitiveValueQuote != '\0')
                {
                    if (_sensitiveValueEscapePending)
                    {
                        _sensitiveValueEscapePending = false;
                        continue;
                    }
                    if (character == '\\')
                    {
                        _sensitiveValueEscapePending = true;
                        continue;
                    }
                    if (character == _sensitiveValueQuote)
                        return index;
                    continue;
                }
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
