using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Media
{
    /// <summary>Writes standard PNG tEXt metadata without decoding the image.</summary>
    internal static class PngParameterWriter
    {
        private static ReadOnlySpan<byte> IendChunk => [0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130];

        internal static void Write(string path, string json, bool checkCanonicalTrailer = false)
        {
            byte[] payload = Encoding.ASCII.GetBytes("ColorVision\0" + json);
            byte[] type = Encoding.ASCII.GetBytes("tEXt");
            using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Span<byte> header = stackalloc byte[8];
            stream.ReadExactly(header);
            if (!header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("Invalid PNG signature.");

            // Freshly encoded PNG files normally end with a canonical IEND.
            // A trailing extension still uses the first IEND found by walking chunks.
            if (!checkCanonicalTrailer || !TrySeekCanonicalTrailer(stream))
                SeekEndChunk(stream, header);

            BinaryPrimitives.WriteUInt32BigEndian(header[..4], (uint)payload.Length);
            type.CopyTo(header[4..]);
            stream.Write(header);
            stream.Write(payload);
            uint crc = uint.MaxValue;
            foreach (byte value in type) crc = UpdateCrc(crc, value);
            foreach (byte value in payload) crc = UpdateCrc(crc, value);
            BinaryPrimitives.WriteUInt32BigEndian(header[..4], ~crc);
            stream.Write(header[..4]);
            stream.Write(IendChunk);
            stream.SetLength(stream.Position);
            stream.Flush(flushToDisk: true);
        }

        private static bool TrySeekCanonicalTrailer(FileStream stream)
        {
            long start = stream.Position;
            long trailerPosition = stream.Length - IendChunk.Length;
            if (trailerPosition < start)
                return false;

            Span<byte> trailer = stackalloc byte[IendChunk.Length];
            stream.Position = trailerPosition;
            stream.ReadExactly(trailer);
            if (trailer.SequenceEqual(IendChunk))
            {
                stream.Position = trailerPosition;
                return true;
            }

            stream.Position = start;
            return false;
        }

        private static void SeekEndChunk(FileStream stream, Span<byte> header)
        {
            while (true)
            {
                long insertion = stream.Position;
                stream.ReadExactly(header);
                uint length = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
                if (length > stream.Length - stream.Position - 4)
                    throw new InvalidDataException("Invalid PNG chunk size.");
                if (header[4..].SequenceEqual("IEND"u8))
                {
                    stream.Position = insertion;
                    return;
                }
                stream.Seek((long)length + 4, SeekOrigin.Current);
            }
        }

        private static uint UpdateCrc(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
            return crc;
        }
    }
}
