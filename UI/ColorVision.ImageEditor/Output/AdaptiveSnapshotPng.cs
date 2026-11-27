using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output;

/// <summary>Lossless PNG encoding for large, mostly uniform result images with at most one alpha level of coverage loss.</summary>
internal static class AdaptiveSnapshotPng
{
    private const int StripCount = 16;
    private const int StripHeight = 16;
    private const int SampleHeight = StripCount * StripHeight;
    private const int BatchHeight = 32;
    private const int MinimumFullSizePixels = 16 * 1024 * 1024;
    private const int MinimumHalfSizePixels = 14 * 1024 * 1024;
    private const int HalfSizeSamplePixelsPerByte = 12;
    private const int MaxCompressedBytes = 32 * 1024 * 1024;
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateCrcTable();

    internal static EncodedPng? TryEncode(BitmapFrame frame, bool halfSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        int minimumPixels = halfSize ? MinimumHalfSizePixels : MinimumFullSizePixels;
        if (frame.Format != PixelFormats.Pbgra32 || width < 2 || (long)width * height < minimumPixels
            || height < SampleHeight || (long)width * SampleHeight * 4 > 16 * 1024 * 1024
            || frame.ColorContexts is { Count: > 0 } || frame.Thumbnail != null
            || !HasMostlyUniformPixels(frame, cancellationToken))
            return null;

        int stride = width * 4;
        byte[] scratch = GC.AllocateUninitializedArray<byte>(stride * SampleHeight);
        BitmapSource sample = CaptureSample(frame, scratch, stride, cancellationToken);
        int referenceSize = GetWpfCompressedSize(sample);
        // On half-size frames the WPF encoder is already fast. Dense samples can compress
        // to a similar size in both encoders even when the adaptive encoder takes longer.
        if (halfSize && referenceSize > width * SampleHeight / HalfSizeSamplePixelsPerByte)
            return null;

        double sizeCeiling = referenceSize * (height / (double)SampleHeight) * 1.20;
        if (referenceSize == 0 || sizeCeiling > MaxCompressedBytes)
            return null;

        // Nearby color equality alone misses low-density noise. Compare the actual encoders
        // over several strips and check the extrapolated size again during the full encode.
        int sampleCeiling = referenceSize + referenceSize / 25;
        // BitmapSource.Create copies the sample into its own bitmap. Reuse the capture
        // buffer for the raw and filtered rows of both compression passes.
        if (Compress(sample, sampleCeiling, scratch, cancellationToken).Count == 0)
            return null;

        byte[]? ancillary = GetWpfAncillaryChunks(frame);
        if (ancillary == null)
            return null;

        ArraySegment<byte> compressed = Compress(frame, (int)sizeCeiling, scratch, cancellationToken);
        return compressed.Count == 0 ? null : new EncodedPng(width, height, ancillary, compressed);
    }

    private static bool HasMostlyUniformPixels(BitmapSource frame, CancellationToken cancellationToken)
    {
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        int[] pair = new int[width * 2];
        const int sampleRows = 64, sampleColumns = 128;
        int uniformPixels = 0;
        for (int row = 0; row < sampleRows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int y = 1 + (int)((long)(height - 2) * row / (sampleRows - 1));
            frame.CopyPixels(new Int32Rect(0, y - 1, width, 2), pair, width * 4, 0);
            for (int column = 0; column < sampleColumns; column++)
            {
                int x = 1 + (width - 2) * column / (sampleColumns - 1);
                int pixel = pair[width + x];
                if ((uint)pixel >> 24 < 254)
                    return false;
                if (pixel == pair[x] || pixel == pair[width + x - 1])
                    uniformPixels++;
            }
        }
        return uniformPixels * 20 >= sampleRows * sampleColumns * 19;
    }

    private static BitmapSource CaptureSample(BitmapSource frame, byte[] pixels, int stride, CancellationToken cancellationToken)
    {
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        for (int strip = 0; strip < StripCount; strip++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int from = (int)((long)height * strip / StripCount);
            int to = (int)((long)height * (strip + 1) / StripCount);
            // Vary the offset within each band to avoid sampling the same periodic row pattern.
            int top = from + (strip * 271 + 43) % (to - from - StripHeight + 1);
            frame.CopyPixels(new Int32Rect(0, top, width, StripHeight), pixels, stride, strip * stride * StripHeight);
        }
        return BitmapSource.Create(width, SampleHeight, frame.DpiX, frame.DpiY, frame.Format, null, pixels, stride);
    }

    private static int GetWpfCompressedSize(BitmapSource sample)
    {
        using MemoryStream png = new();
        EncodeWpf(BitmapFrame.Create(sample), png);
        ReadOnlySpan<byte> data = png.GetBuffer().AsSpan(0, (int)png.Length);
        int bytes = 0;
        for (int offset = Signature.Length; offset < data.Length; offset += 12 + ReadChunkLength(data, offset))
            if (ReadChunkType(data, offset).SequenceEqual("IDAT"u8))
                bytes += ReadChunkLength(data, offset);
        return bytes;
    }

    private static byte[]? GetWpfAncillaryChunks(BitmapFrame frame)
    {
        BitmapMetadata? metadata = frame.Metadata as BitmapMetadata;
        if (metadata != null && metadata.Format != "png")
            return null;

        // The full frame already contains the correctly sized ColorVision record. Encode
        // a single pixel through WPF to preserve the original metadata chunks and DPI rounding.
        BitmapSource tiny = BitmapSource.Create(1, 1, frame.DpiX, frame.DpiY, frame.Format, null,
            new byte[] { 0, 0, 0, 255 }, 4);
        using MemoryStream png = new();
        EncodeWpf(BitmapFrame.Create(tiny, null, metadata, null), png);
        ReadOnlySpan<byte> data = png.GetBuffer().AsSpan(0, (int)png.Length);
        const int firstChunk = 33;
        if (data.Length < firstChunk || !data[..8].SequenceEqual(Signature)
            || !ReadChunkType(data, 8).SequenceEqual("IHDR"u8)
            || !data.Slice(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 }))
            return null;

        int firstIdat = 0;
        for (int offset = firstChunk; offset < data.Length; offset += 12 + ReadChunkLength(data, offset))
        {
            ReadOnlySpan<byte> type = ReadChunkType(data, offset);
            if (type.SequenceEqual("IDAT"u8))
            {
                if (firstIdat == 0)
                    firstIdat = offset;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                return firstIdat == 0 ? null : data[firstChunk..firstIdat].ToArray();
            }
            else if (firstIdat != 0 || (!type.SequenceEqual("sRGB"u8) && !type.SequenceEqual("gAMA"u8)
                && !type.SequenceEqual("pHYs"u8)
                && !(type.SequenceEqual("tEXt"u8) && data.Slice(offset + 8, ReadChunkLength(data, offset)).StartsWith("ColorVision\0"u8))))
            {
                return null;
            }
        }
        return null;
    }

    private static void EncodeWpf(BitmapFrame frame, Stream output)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(frame);
        encoder.Save(output);
    }

    private static int ReadChunkLength(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));

    private static ReadOnlySpan<byte> ReadChunkType(ReadOnlySpan<byte> data, int offset) => data.Slice(offset + 4, 4);

    private static ArraySegment<byte> Compress(BitmapSource frame, int sizeCeiling, byte[] scratch, CancellationToken cancellationToken)
    {
        int width = frame.PixelWidth;
        int stride = width * 4;
        int batchBytes = stride * BatchHeight;
        int filteredStride = stride + 1;
        Span<byte> previous = scratch.AsSpan(batchBytes, stride);
        Span<byte> filtered = scratch.AsSpan(batchBytes + stride, filteredStride * BatchHeight);
        previous.Clear();
        BitmapSource? straightAlpha = null;
        using MemoryStream compressed = new();
        using (ZLibStream writer = new(compressed, new ZLibCompressionOptions { CompressionLevel = 6 }, leaveOpen: true))
        {
            for (int top = 0; top < frame.PixelHeight; top += BatchHeight)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int rows = Math.Min(BatchHeight, frame.PixelHeight - top);
                Int32Rect region = new(0, top, width, rows);
                frame.CopyPixels(region, scratch, stride, 0);
                Span<byte> pixels = scratch.AsSpan(0, rows * stride);
                ReadOnlySpan<uint> packed = MemoryMarshal.Cast<byte, uint>(pixels);
                if (!IsOpaque(packed))
                {
                    foreach (uint pixel in packed)
                        if (pixel >> 24 < 254)
                            return default;

                    // WPF can round the alpha of antialiased marks to 254 over an opaque base.
                    // Use the same WIC straight-alpha conversion as the PNG encoder for those rows.
                    straightAlpha ??= new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                    straightAlpha.CopyPixels(region, scratch, stride, 0);
                }

                for (int row = 0; row < rows; row++)
                {
                    ReadOnlySpan<byte> raw = pixels.Slice(row * stride, stride);
                    ReadOnlySpan<byte> prior = row == 0 ? previous : pixels.Slice((row - 1) * stride, stride);
                    Span<byte> output = filtered.Slice(row * filteredStride, filteredStride);
                    int filter = SelectFilter(raw, prior);
                    output[0] = (byte)filter;
                    FilterRow(raw, prior, output[1..], filter);
                }
                pixels.Slice((rows - 1) * stride, stride).CopyTo(previous);
                writer.Write(filtered[..(rows * filteredStride)]);
                if (compressed.Length > sizeCeiling)
                    return default;
            }
        }
        return compressed.Length <= sizeCeiling
            ? new ArraySegment<byte>(compressed.GetBuffer(), 0, (int)compressed.Length)
            : default;
    }

    private static bool IsOpaque(ReadOnlySpan<uint> pixels)
    {
        const uint alphaMask = 0xFF000000;
        Vector<uint> alpha = new(alphaMask);
        while (pixels.Length >= Vector<uint>.Count)
        {
            if ((new Vector<uint>(pixels) & alpha) != alpha)
                return false;
            pixels = pixels[Vector<uint>.Count..];
        }
        foreach (uint pixel in pixels)
            if ((pixel & alphaMask) != alphaMask)
                return false;
        return true;
    }

    private static int SelectFilter(ReadOnlySpan<byte> raw, ReadOnlySpan<byte> previous)
    {
        int none = 0, sub = 0, up = 0;
        for (int i = 4; i + 4 <= raw.Length; i += 64)
        {
            for (int offset = 0; offset < 4; offset++)
            {
                int pixel = i + offset;
                int value = raw[pixel];
                none += Math.Abs((int)(sbyte)value);
                sub += Math.Abs((int)(sbyte)(value - raw[pixel - 4]));
                up += Math.Abs((int)(sbyte)(value - previous[pixel]));
            }
        }
        return up <= none && up <= sub ? 2 : sub <= none ? 1 : 0;
    }

    private static unsafe void FilterRow(ReadOnlySpan<byte> raw, ReadOnlySpan<byte> prior, Span<byte> output, int filter)
    {
        int i = 0;
        if (filter == 1)
        {
            // Sub's first pixel is unfiltered because no left pixel exists.
            output[0] = raw[2]; output[1] = raw[1]; output[2] = raw[0]; output[3] = raw[3];
            i = 4;
        }
        if (Ssse3.IsSupported)
        {
            Vector128<byte> rgba = Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);
            fixed (byte* input = raw, previous = prior, destination = output)
            {
                for (; i + 16 <= raw.Length; i += 16)
                {
                    Vector128<byte> value = Sse2.LoadVector128(input + i);
                    if (filter == 2)
                        value = Sse2.Subtract(value, Sse2.LoadVector128(previous + i));
                    else if (filter == 1)
                        value = Sse2.Subtract(value, Sse2.LoadVector128(input + i - 4));
                    Sse2.Store(destination + i, Ssse3.Shuffle(value, rgba));
                }
            }
        }
        for (; i < raw.Length; i += 4)
        {
            for (int component = 0; component < 4; component++)
            {
                int pixel = i + (component < 3 ? 2 - component : 3);
                int reference = filter == 2 ? prior[pixel] : filter == 1 ? raw[pixel - 4] : 0;
                output[i + component] = (byte)(raw[pixel] - reference);
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
        uint crc = AppendCrc(AppendCrc(uint.MaxValue, type), data);
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
        output.Write(value);
    }

    private static uint AppendCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
            crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 0xFF];
        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
            table[i] = crc;
        }
        return table;
    }

    internal sealed class EncodedPng(int width, int height, byte[] ancillary, ArraySegment<byte> compressed)
    {
        internal void Write(Stream output)
        {
            output.Write(Signature);
            Span<byte> header = stackalloc byte[13];
            header.Clear();
            BinaryPrimitives.WriteInt32BigEndian(header, width);
            BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
            header[8] = 8;
            header[9] = 6;
            WriteChunk(output, "IHDR"u8, header);
            output.Write(ancillary);
            ReadOnlySpan<byte> data = compressed.AsSpan();
            while (!data.IsEmpty)
            {
                int length = Math.Min(data.Length, 64 * 1024);
                WriteChunk(output, "IDAT"u8, data[..length]);
                data = data[length..];
            }
            WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        }
    }
}
