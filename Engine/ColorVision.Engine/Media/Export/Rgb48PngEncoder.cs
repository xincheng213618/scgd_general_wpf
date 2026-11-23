using OpenCvSharp;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ColorVision.Engine.Media;

/// <summary>Streams the automatic 16-bit color PNG representation from an immutable BGR matrix.</summary>
internal static class Rgb48PngEncoder
{
    private const int BytesPerPixel = 6;
    private const int IdatChunkSize = 256 * 1024;
    private static readonly uint[] CrcTable = CreateCrcTable();

    internal static unsafe void Write(string path, Mat source)
    {
        int width = source.Cols;
        int height = source.Rows;
        long stride = source.Step();
        int rowBytes = checked(width * BytesPerPixel);
        int filteredStride = checked(rowBytes + 1);
        int batchRows = Math.Max(1, Math.Min(16, (1024 * 1024) / filteredStride));
        byte[] rows = GC.AllocateUninitializedArray<byte>(batchRows * filteredStride);
        byte* pixels = (byte*)source.Data;

        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024);
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 16;
        header[9] = 2; // RGB
        WriteChunk(output, "IHDR"u8, header);

        using IdatStream idat = new(output);
        // Match OpenCV's automatic PNG policy while combining channel order, byte order and Sub filtering.
        ZLibCompressionOptions options = new() { CompressionLevel = 1, CompressionStrategy = ZLibCompressionStrategy.RunLengthEncoding };
        using (ZLibStream zlib = new(idat, options, leaveOpen: true))
        {
            for (int top = 0; top < height;)
            {
                int count = Math.Min(batchRows, height - top);
                fixed (byte* buffer = rows)
                {
                    for (int row = 0; row < count; row++)
                    {
                        byte* filtered = buffer + row * filteredStride;
                        filtered[0] = 1; // Sub
                        FilterRow(pixels + (top + row) * stride, filtered + 1, rowBytes);
                    }
                }
                zlib.Write(rows.AsSpan(0, count * filteredStride));
                top += count;
            }
        }
        idat.CompleteChunk();
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        GC.KeepAlive(source);
    }

    private static unsafe void FilterRow(byte* source, byte* output, int length)
    {
        int offset = 0;
        for (int channel = 0; channel < BytesPerPixel; channel++)
            output[channel] = source[BytesPerPixel - 1 - channel];
        offset += BytesPerPixel;

        Vector128<byte> bigEndianRgb = Vector128.Create((byte)5, 4, 3, 2, 1, 0, 11, 10, 9, 8, 7, 6, 128, 128, 128, 128);
        // A vector writes two RGB pixels. The next iteration or scalar tail overwrites the last four lanes.
        for (; offset <= length - 16; offset += 2 * BytesPerPixel)
        {
            Vector128<byte> value = Sse2.LoadVector128(source + offset);
            Vector128<byte> left = Sse2.LoadVector128(source + offset - BytesPerPixel);
            Sse2.Store(output + offset, Ssse3.Shuffle(Sse2.Subtract(value, left), bigEndianRgb));
        }

        for (; offset < length; offset += BytesPerPixel)
        {
            for (int channel = 0; channel < BytesPerPixel; channel++)
            {
                int index = offset + BytesPerPixel - 1 - channel;
                output[offset + channel] = unchecked((byte)(source[index] - source[index - BytesPerPixel]));
            }
        }
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(value, data.Length);
        output.Write(value);
        output.Write(type);
        output.Write(data);

        uint crc = uint.MaxValue;
        foreach (byte b in type)
            crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 255];
        // Independent lookups for each eight-byte block avoid a per-byte dependency on the previous CRC.
        int index = 0;
        for (; index + 8 <= data.Length; index += 8)
        {
            ulong block = BinaryPrimitives.ReadUInt64LittleEndian(data[index..]) ^ crc;
            crc = CrcTable[7 * 256 + (byte)block] ^ CrcTable[6 * 256 + (byte)(block >> 8)]
                ^ CrcTable[5 * 256 + (byte)(block >> 16)] ^ CrcTable[4 * 256 + (byte)(block >> 24)]
                ^ CrcTable[3 * 256 + (byte)(block >> 32)] ^ CrcTable[2 * 256 + (byte)(block >> 40)]
                ^ CrcTable[1 * 256 + (byte)(block >> 48)] ^ CrcTable[(byte)(block >> 56)];
        }
        foreach (byte b in data[index..])
            crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 255];
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
        output.Write(value);
    }

    private static uint[] CreateCrcTable()
    {
        uint[] table = new uint[8 * 256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
            table[i] = crc;
        }
        for (int slice = 1; slice < 8; slice++)
        {
            for (int i = 0; i < 256; i++)
            {
                uint previous = table[(slice - 1) * 256 + i];
                table[slice * 256 + i] = (previous >> 8) ^ table[previous & 255];
            }
        }
        return table;
    }

    private sealed class IdatStream(Stream output) : Stream
    {
        private readonly byte[] buffer = new byte[IdatChunkSize];
        private int used;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] source, int offset, int count) => Write(source.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> source)
        {
            while (!source.IsEmpty)
            {
                int count = Math.Min(source.Length, buffer.Length - used);
                source[..count].CopyTo(buffer.AsSpan(used));
                used += count;
                source = source[count..];
                if (used == buffer.Length)
                    CompleteChunk();
            }
        }

        internal void CompleteChunk()
        {
            if (used == 0)
                return;
            WriteChunk(output, "IDAT"u8, buffer.AsSpan(0, used));
            used = 0;
        }

        public override void Flush() { }
        public override int Read(byte[] destination, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
