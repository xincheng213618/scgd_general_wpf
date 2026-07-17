using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotBoundedTextLineReader : IDisposable
    {
        private const int CharacterBufferSize = 4096;
        private readonly StreamReader _reader;
        private readonly char[] _characterBuffer = new char[CharacterBufferSize];
        private readonly int _maximumLineCharacters;
        private readonly string _contentLabel;
        private int _bufferOffset;
        private int _bufferCount;
        private bool _skipLineFeed;

        public CopilotBoundedTextLineReader(
            Stream stream,
            Encoding encoding,
            int maximumBytes,
            int maximumLineCharacters,
            string contentLabel)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(encoding);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLineCharacters);

            _maximumLineCharacters = maximumLineCharacters;
            _contentLabel = string.IsNullOrWhiteSpace(contentLabel) ? "Text stream" : contentLabel.Trim();
            _reader = new StreamReader(
                new BoundedReadStream(stream, maximumBytes, _contentLabel),
                encoding,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: CharacterBufferSize,
                leaveOpen: false);
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = new StringBuilder(Math.Min(1024, _maximumLineCharacters));
            var skipLineFeed = _skipLineFeed;
            _skipLineFeed = false;

            while (true)
            {
                if (_bufferOffset >= _bufferCount)
                {
                    _bufferCount = await _reader.ReadAsync(_characterBuffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    _bufferOffset = 0;
                    if (_bufferCount == 0)
                        return line.Length == 0 ? null : line.ToString();
                }

                while (_bufferOffset < _bufferCount)
                {
                    var character = _characterBuffer[_bufferOffset++];
                    if (skipLineFeed)
                    {
                        skipLineFeed = false;
                        if (character == '\n')
                            continue;
                    }

                    if (character == '\r')
                    {
                        _skipLineFeed = true;
                        return line.ToString();
                    }

                    if (character == '\n')
                        return line.ToString();

                    if (line.Length >= _maximumLineCharacters)
                    {
                        throw new InvalidOperationException(
                            $"{_contentLabel} line exceeded the character limit ({_maximumLineCharacters}).");
                    }

                    line.Append(character);
                }
            }
        }

        public void Dispose()
        {
            _reader.Dispose();
        }

        private sealed class BoundedReadStream(Stream inner, int maximumBytes, string contentLabel) : Stream
        {
            private long _bytesRead;

            public override bool CanRead => inner.CanRead;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => _bytesRead;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = inner.Read(buffer, offset, GetAllowedReadCount(count));
                CountBytes(bytesRead);
                return bytesRead;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var bytesRead = await inner.ReadAsync(buffer[..GetAllowedReadCount(buffer.Length)], cancellationToken).ConfigureAwait(false);
                CountBytes(bytesRead);
                return bytesRead;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return ReadArrayAsync(buffer, offset, count, cancellationToken);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            private int GetAllowedReadCount(int requestedCount)
            {
                if (requestedCount <= 0)
                    return 0;

                var remainingBytes = maximumBytes - _bytesRead;
                return (int)Math.Min(requestedCount, Math.Max(1, remainingBytes + 1));
            }

            private void CountBytes(int bytesRead)
            {
                if (bytesRead > maximumBytes - _bytesRead)
                {
                    throw new InvalidOperationException(
                        $"{contentLabel} exceeded the size limit ({maximumBytes / 1024} KB).");
                }

                _bytesRead += bytesRead;
            }

            private async Task<int> ReadArrayAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesRead = await inner.ReadAsync(buffer.AsMemory(offset, GetAllowedReadCount(count)), cancellationToken).ConfigureAwait(false);
                CountBytes(bytesRead);
                return bytesRead;
            }
        }
    }
}
