using System.IO;
using System.Text;

namespace ColorVision.SocketProtocol;

internal sealed class SocketTextMessageFramer : IDisposable
{
    private const int MaxJsonBytes = 64 * 1024;
    private readonly MemoryStream _json = new();
    private int _depth;
    private bool _inString;
    private bool _escaped;

    internal IEnumerable<string> Read(byte[] buffer, int length)
    {
        int offset = 0;
        while (offset < length)
        {
            int start = offset;
            if (_depth == 0)
            {
                while (offset < length && buffer[offset] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') offset++;
                if (offset == length) yield break;
                if (buffer[offset] != (byte)'{')
                {
                    int separator = Array.IndexOf(buffer, (byte)';', offset, length - offset);
                    offset = separator < 0 ? length : separator + 1;
                    while (offset < length && buffer[offset] == (byte)';') offset++;
                    yield return Encoding.UTF8.GetString(buffer, start, offset - start);
                    continue;
                }
                start = offset;
            }

            do { ScanJsonByte(buffer[offset++]); }
            while (offset < length && _depth > 0);

            if (_json.Length + offset - start > MaxJsonBytes)
                throw new InvalidDataException($"Socket JSON object exceeds {MaxJsonBytes} bytes.");

            _json.Write(buffer, start, offset - start);
            if (_depth == 0)
            {
                string message = Encoding.UTF8.GetString(_json.GetBuffer(), 0, (int)_json.Length);
                _json.SetLength(0);
                yield return message;
            }
        }
    }

    private void ScanJsonByte(byte value)
    {
        if (_inString)
        {
            if (_escaped) _escaped = false;
            else if (value == (byte)'\\') _escaped = true;
            else if (value == (byte)'"') _inString = false;
        }
        else if (value == (byte)'"') _inString = true;
        else if (value == (byte)'{') _depth++;
        else if (value == (byte)'}') _depth--;
    }

    public void Dispose() => _json.Dispose();
}
