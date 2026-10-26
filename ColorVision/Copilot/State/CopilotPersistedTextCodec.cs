using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotPersistedTextCodec
    {
        private static readonly uint[] Crc32Table = CreateCrc32Table();
        private static readonly UTF8Encoding StrictUtf8 = new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        public static string Encode(
            string? value,
            string prefix,
            int minimumCompressionCharacters,
            int maximumDecodedCharacters)
        {
            ValidateArguments(prefix, minimumCompressionCharacters, maximumDecodedCharacters);
            var text = value ?? string.Empty;
            var mustEscapePrefix = text.StartsWith(prefix, StringComparison.Ordinal);
            if (text.Length > maximumDecodedCharacters
                || (!mustEscapePrefix && text.Length < minimumCompressionCharacters))
            {
                return text;
            }

            var uncompressedBytes = Encoding.UTF8.GetBytes(text);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(uncompressedBytes);
            }

            var payload = prefix + Convert.ToBase64String(output.ToArray());
            return mustEscapePrefix || payload.Length < text.Length ? payload : text;
        }

        public static string Decode(
            string? value,
            string prefix,
            int maximumDecodedCharacters)
        {
            ValidateArguments(
                prefix,
                minimumCompressionCharacters: 0,
                maximumDecodedCharacters: maximumDecodedCharacters);
            var payload = value ?? string.Empty;
            if (!payload.StartsWith(prefix, StringComparison.Ordinal)
                || (long)payload.Length > (long)maximumDecodedCharacters + prefix.Length)
            {
                return payload;
            }

            try
            {
                var compressedBytes = Convert.FromBase64String(payload[prefix.Length..]);
                if (compressedBytes.Length < 20)
                    return payload;

                using var input = new MemoryStream(compressedBytes, writable: false);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                var buffer = new byte[8192];
                var maximumDecodedBytes = (long)maximumDecodedCharacters * 4;
                while (true)
                {
                    var bytesRead = gzip.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;
                    if (output.Length + bytesRead > maximumDecodedBytes)
                        return payload;

                    output.Write(buffer, 0, bytesRead);
                }

                var decodedBytes = output.GetBuffer().AsSpan(0, checked((int)output.Length));
                // GZipStream can return partial content at EOF without requiring a complete trailer.
                // The persisted v1 format contains one gzip member, including its CRC32 and ISIZE.
                if (BinaryPrimitives.ReadUInt32LittleEndian(compressedBytes.AsSpan(compressedBytes.Length - 4)) != (uint)decodedBytes.Length
                    || BinaryPrimitives.ReadUInt32LittleEndian(compressedBytes.AsSpan(compressedBytes.Length - 8)) != ComputeCrc32(decodedBytes))
                {
                    return payload;
                }

                var decoded = StrictUtf8.GetString(decodedBytes);
                return decoded.Length <= maximumDecodedCharacters ? decoded : payload;
            }
            catch (Exception exception) when (exception is FormatException
                or InvalidDataException
                or IOException
                or ArgumentException
                or DecoderFallbackException)
            {
                return payload;
            }
        }

        public static string RetainOrEncode(
            string payload,
            string decoded,
            string prefix,
            int minimumCompressionCharacters,
            int maximumDecodedCharacters)
        {
            return payload.StartsWith(prefix, StringComparison.Ordinal)
                && !string.Equals(payload, decoded, StringComparison.Ordinal)
                    ? payload
                    : Encode(decoded, prefix, minimumCompressionCharacters, maximumDecodedCharacters);
        }

        private static void ValidateArguments(
            string prefix,
            int minimumCompressionCharacters,
            int maximumDecodedCharacters)
        {
            ArgumentException.ThrowIfNullOrEmpty(prefix);
            ArgumentOutOfRangeException.ThrowIfNegative(minimumCompressionCharacters);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDecodedCharacters);
        }

        private static uint ComputeCrc32(ReadOnlySpan<byte> data)
        {
            var crc = uint.MaxValue;
            foreach (var value in data)
                crc = Crc32Table[(crc ^ value) & byte.MaxValue] ^ (crc >> 8);
            return ~crc;
        }

        private static uint[] CreateCrc32Table()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                table[index] = value;
            }
            return table;
        }
    }
}
