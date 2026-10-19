using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ProjectARVRPro.IntegrationDemo
{
    internal sealed class JsonStreamMessageReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[8192];
        private readonly char[] _charBuffer = new char[Encoding.UTF8.GetMaxCharCount(8192)];
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
        private readonly StringBuilder _textBuffer = new StringBuilder();
        private int _scanIndex;
        private int _objectStart = -1;
        private int _depth;
        private bool _inString;
        private bool _escaped;

        public JsonStreamMessageReader(Stream stream)
        {
            _stream = stream;
        }

        public async Task<string> ReadMessageAsync()
        {
            while (true)
            {
                string json;
                if (TryExtractJsonObject(out json))
                    return json;

                int bytesRead = await _stream.ReadAsync(_buffer, 0, _buffer.Length);
                if (bytesRead == 0)
                {
                    int finalChars = _decoder.GetChars(_buffer, 0, 0, _charBuffer, 0, true);
                    if (finalChars > 0)
                        _textBuffer.Append(_charBuffer, 0, finalChars);
                    return _textBuffer.Length == 0 ? null : _textBuffer.ToString();
                }

                int charsDecoded = _decoder.GetChars(_buffer, 0, bytesRead, _charBuffer, 0, false);
                _textBuffer.Append(_charBuffer, 0, charsDecoded);
            }
        }

        private bool TryExtractJsonObject(out string json)
        {
            json = null;

            // Keep framing state across reads so multi-megabyte file payloads are scanned once.
            for (; _scanIndex < _textBuffer.Length; _scanIndex++)
            {
                char current = _textBuffer[_scanIndex];
                if (_objectStart < 0)
                {
                    if (char.IsWhiteSpace(current))
                        continue;

                    if (current == '{')
                    {
                        _objectStart = _scanIndex;
                        _depth = 1;
                        continue;
                    }

                    throw new InvalidDataException("Unexpected character before JSON object: " + current);
                }

                if (_inString)
                {
                    if (_escaped)
                    {
                        _escaped = false;
                        continue;
                    }

                    if (current == '\\')
                    {
                        _escaped = true;
                        continue;
                    }

                    if (current == '"')
                        _inString = false;

                    continue;
                }

                if (current == '"')
                {
                    _inString = true;
                    continue;
                }

                if (current == '{')
                {
                    _depth++;
                    continue;
                }

                if (current == '}')
                {
                    _depth--;
                    if (_depth == 0)
                    {
                        int length = _scanIndex - _objectStart + 1;
                        json = _textBuffer.ToString(_objectStart, length);
                        _textBuffer.Remove(0, _scanIndex + 1);
                        _scanIndex = 0;
                        _objectStart = -1;
                        _inString = false;
                        _escaped = false;
                        return true;
                    }
                }
            }

            if (_objectStart > 0)
            {
                _textBuffer.Remove(0, _objectStart);
                _scanIndex -= _objectStart;
                _objectStart = 0;
            }
            else if (_objectStart < 0)
            {
                _textBuffer.Clear();
                _scanIndex = 0;
            }

            return false;
        }

        public async Task<string> ReadMessageAsync(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
                return await ReadMessageAsync();

            Task<string> readTask = ReadMessageAsync();
            Task completedTask = await Task.WhenAny(readTask, Task.Delay(timeout));
            if (completedTask == readTask)
                return await readTask;

            _ = readTask.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            try
            {
                _stream.Dispose();
            }
            catch
            {
            }

            throw new TimeoutException("No complete JSON message received in " + timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " seconds.");
        }
    }
}
