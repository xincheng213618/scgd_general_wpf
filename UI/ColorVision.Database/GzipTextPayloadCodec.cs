using System;
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
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static GzipTextPayload Encode(string? text)
        {
            if (text == null)
                return new GzipTextPayload(null, null);

            byte[] utf8 = StrictUtf8.GetBytes(text);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(utf8, 0, utf8.Length);
            return new GzipTextPayload(output.ToArray(), utf8.Length);
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
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int read = gzip.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                        break;
                    if (output.Length + read > maximumUtf8Bytes)
                        throw new InvalidDataException($"解压后的载荷超过允许上限 {maximumUtf8Bytes:N0} 字节。");
                    output.Write(buffer, 0, read);
                }

                byte[] utf8 = output.ToArray();
                if (expectedUtf8Length != null && utf8.Length != expectedUtf8Length.Value)
                    throw new InvalidDataException(
                        $"解压后的 UTF-8 长度不一致，期望 {expectedUtf8Length.Value:N0}，实际 {utf8.Length:N0} 字节。");
                return StrictUtf8.GetString(utf8);
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
