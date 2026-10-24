using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Media
{
    /// <summary>Writes standard PNG tEXt metadata without decoding the image.</summary>
    internal static class PngParameterWriter
    {
        internal static void Write(string path, string json)
        {
            byte[] payload = Encoding.ASCII.GetBytes("ColorVision\0" + json);
            byte[] type = Encoding.ASCII.GetBytes("tEXt");
            using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Span<byte> header = stackalloc byte[8];
            stream.ReadExactly(header);
            if (!header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("Invalid PNG signature.");
            // Insert before IEND. PNG permits ancillary tEXt chunks after IDAT.
            long insertion;
            while (true)
            {
                insertion = stream.Position;
                stream.ReadExactly(header);
                uint length = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
                if (length > stream.Length - stream.Position - 4) throw new InvalidDataException("Invalid PNG chunk size.");
                if (header[4] == 'I' && header[5] == 'E' && header[6] == 'N' && header[7] == 'D') break;
                stream.Seek(checked((long)length + 4), SeekOrigin.Current);
            }
            stream.Position = insertion;
            BinaryPrimitives.WriteUInt32BigEndian(header[..4], (uint)payload.Length);
            type.CopyTo(header[4..]);
            stream.Write(header);
            stream.Write(payload);
            uint crc = uint.MaxValue;
            foreach (byte value in type) crc = UpdateCrc(crc, value);
            foreach (byte value in payload) crc = UpdateCrc(crc, value);
            BinaryPrimitives.WriteUInt32BigEndian(header[..4], ~crc);
            stream.Write(header[..4]);
            stream.Write(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
            stream.SetLength(stream.Position);
            stream.Flush(flushToDisk: true);
        }

        private static uint UpdateCrc(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
            return crc;
        }
    }
}
