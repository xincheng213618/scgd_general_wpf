using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.Database
{
    /// <summary>
    /// Strict UTF-8/GZip codec used by SQLite payload columns.
    /// Null and empty strings remain distinct, and decompression is bounded.
    /// </summary>
    public static class GzipTextPayloadCodec
    {
        public const int DefaultMaximumUtf8Bytes = 64 * 1024 * 1024;
        private const int Utf8BufferSize = 64 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static GzipTextPayload Encode(string? text)
        {
            if (text == null)
                return new GzipTextPayload(null, null);

            int utf8Length;
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                utf8Length = WriteUtf8(gzip, text);
            return new GzipTextPayload(output.ToArray(), utf8Length);
        }

        private static int WriteUtf8(GZipStream gzip, string text)
        {
            if (text.Length <= Utf8BufferSize / 3)
            {
                byte[] utf8 = StrictUtf8.GetBytes(text);
                gzip.Write(utf8);
                return utf8.Length;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(Utf8BufferSize);
            try
            {
                Encoder encoder = StrictUtf8.GetEncoder();
                ReadOnlySpan<char> remaining = text.AsSpan();
                int utf8Length = 0;
                bool completed;
                do
                {
                    encoder.Convert(remaining, buffer.AsSpan(0, Utf8BufferSize), flush: true,
                        out int charsUsed, out int bytesUsed, out completed);
                    gzip.Write(buffer.AsSpan(0, bytesUsed));
                    remaining = remaining[charsUsed..];
                    utf8Length = checked(utf8Length + bytesUsed);
                }
                while (!completed);
                return utf8Length;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static string? Decode(
            byte[]? compressedBytes,
            int? expectedUtf8Length,
            int maximumUtf8Bytes = DefaultMaximumUtf8Bytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumUtf8Bytes);

            if (compressedBytes == null)
            {
                if (expectedUtf8Length != null)
                    throw new InvalidDataException("压缩载荷为空，但记录的 UTF-8 长度不为空。");
                return null;
            }

            if (expectedUtf8Length is < 0)
                throw new InvalidDataException("压缩载荷记录了无效的 UTF-8 长度。");
            if (expectedUtf8Length > maximumUtf8Bytes)
                throw new InvalidDataException($"压缩载荷声明的长度超过允许上限 {maximumUtf8Bytes:N0} 字节。");

            try
            {
                using var input = new MemoryStream(compressedBytes, writable: false);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream(expectedUtf8Length.GetValueOrDefault());
                byte[] buffer = ArrayPool<byte>.Shared.Rent(Utf8BufferSize);
                try
                {
                    int read;
                    while ((read = gzip.Read(buffer, 0, Utf8BufferSize)) != 0)
                    {
                        if (output.Length + read > maximumUtf8Bytes)
                            throw new InvalidDataException($"解压后的载荷超过允许上限 {maximumUtf8Bytes:N0} 字节。");
                        output.Write(buffer, 0, read);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                int utf8Length = checked((int)output.Length);
                if (expectedUtf8Length != null && utf8Length != expectedUtf8Length.Value)
                    throw new InvalidDataException(
                        $"解压后的 UTF-8 长度不一致，期望 {expectedUtf8Length.Value:N0}，实际 {utf8Length:N0} 字节。");
                return StrictUtf8.GetString(output.GetBuffer(), 0, utf8Length);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or DecoderFallbackException)
            {
                throw new InvalidDataException("GZip 文本载荷损坏或不是有效的 UTF-8。", ex);
            }
        }

        public static string? CreatePreview(string? text, int maximumCharacters = 256)
        {
            if (text == null)
                return null;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCharacters);
            if (text.Length <= maximumCharacters)
                return text;

            int length = maximumCharacters;
            if (length > 0 && char.IsHighSurrogate(text[length - 1]))
                length--;
            return string.Concat(text.AsSpan(0, length), "…");
        }
    }

    public readonly record struct GzipTextPayload(byte[]? CompressedBytes, int? Utf8Length);
}
