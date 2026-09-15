using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectARVRPro.SemiAuto.GECS
{
    public sealed class GecsFrame
    {
        public GecsFrame(byte networkNumber, string messageText)
        {
            NetworkNumber = networkNumber;
            MessageText = messageText ?? string.Empty;
        }

        public byte NetworkNumber { get; private set; }

        public string MessageText { get; private set; }
    }

    public static class GecsPacketCodec
    {
        public const byte Stx = 0x02;
        public const byte Etx = 0x03;
        public const int MaximumMessageLength = 0xFFFF;

        public static byte[] BuildPacket(string messageText, byte networkNumber)
        {
            if (messageText == null)
                throw new ArgumentNullException(nameof(messageText));
            if (messageText.Any(character => character > 0x7F))
                throw new ArgumentException("GECS Message Text 只能包含 ASCII 字符。", nameof(messageText));

            byte[] messageBytes = Encoding.ASCII.GetBytes(messageText);
            if (messageBytes.Length > MaximumMessageLength)
                throw new ArgumentOutOfRangeException(nameof(messageText), "GECS Message Text 长度不能超过 FFFF。 ");

            byte[] lengthBytes = Encoding.ASCII.GetBytes(messageBytes.Length.ToString("X4", CultureInfo.InvariantCulture));
            byte[] packet = new byte[messageBytes.Length + 7];
            packet[0] = Stx;
            packet[1] = networkNumber;
            Buffer.BlockCopy(lengthBytes, 0, packet, 2, lengthBytes.Length);
            Buffer.BlockCopy(messageBytes, 0, packet, 6, messageBytes.Length);
            packet[packet.Length - 1] = Etx;
            return packet;
        }

        public static bool TryReadFrame(List<byte> buffer, out GecsFrame frame)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            frame = null;
            int stxIndex = buffer.IndexOf(Stx);
            if (stxIndex < 0)
            {
                buffer.Clear();
                return false;
            }

            if (stxIndex > 0)
                buffer.RemoveRange(0, stxIndex);
            if (buffer.Count < 7)
                return false;

            int messageLength = ParseHexLength(buffer, 2);
            int packetLength = messageLength + 7;
            if (buffer.Count < packetLength)
                return false;
            if (buffer[packetLength - 1] != Etx)
            {
                buffer.RemoveAt(0);
                throw new InvalidDataException("GECS 数据帧缺少 ETX(0x03)，已跳过当前 STX。 ");
            }

            byte[] messageBytes = buffer.GetRange(6, messageLength).ToArray();
            if (messageBytes.Any(value => value > 0x7F))
            {
                buffer.RemoveRange(0, packetLength);
                throw new InvalidDataException("GECS Message Text 包含非 ASCII 字节。 ");
            }

            frame = new GecsFrame(buffer[1], Encoding.ASCII.GetString(messageBytes));
            buffer.RemoveRange(0, packetLength);
            return true;
        }

        private static int ParseHexLength(IReadOnlyList<byte> buffer, int startIndex)
        {
            int value = 0;
            for (int index = 0; index < 4; index++)
            {
                byte character = buffer[startIndex + index];
                int digit;
                if (character >= (byte)'0' && character <= (byte)'9')
                    digit = character - (byte)'0';
                else if (character >= (byte)'A' && character <= (byte)'F')
                    digit = character - (byte)'A' + 10;
                else if (character >= (byte)'a' && character <= (byte)'f')
                    digit = character - (byte)'a' + 10;
                else
                    throw new InvalidDataException("GECS Message Length 必须是 4 位 HEX-ASCII。 ");

                value = (value << 4) | digit;
            }

            return value;
        }
    }

    public sealed class GecsFrameReader
    {
        private readonly Stream _stream;
        private readonly byte[] _readBuffer = new byte[8192];
        private readonly List<byte> _frameBuffer = new List<byte>();

        public GecsFrameReader(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public async Task<GecsFrame> ReadFrameAsync(TimeSpan timeout)
        {
            Task<GecsFrame> readTask = ReadFrameCoreAsync();
            if (timeout <= TimeSpan.Zero)
                return await readTask;

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

            throw new TimeoutException("在 " + timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " 秒内未收到完整 GECS 数据帧。 ");
        }

        private async Task<GecsFrame> ReadFrameCoreAsync()
        {
            while (true)
            {
                GecsFrame frame;
                if (GecsPacketCodec.TryReadFrame(_frameBuffer, out frame))
                    return frame;

                int bytesRead = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);
                if (bytesRead == 0)
                {
                    if (_frameBuffer.Count == 0)
                        return null;
                    throw new EndOfStreamException("GECS 连接在完整数据帧到达前已关闭。 ");
                }

                for (int index = 0; index < bytesRead; index++)
                    _frameBuffer.Add(_readBuffer[index]);
            }
        }
    }
}
