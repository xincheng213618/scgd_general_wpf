using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.FileIO
{
    /// <summary>A versioned, opaque parameter value. Unknown kinds and versions survive updates.</summary>
    public sealed class CVFileProperty
    {
        public uint Version { get; }
        public byte[] Value { get; }

        public CVFileProperty(uint version, byte[] value)
        {
            Version = version;
            Value = value == null ? throw new ArgumentNullException(nameof(value)) : (byte[])value.Clone();
        }
    }

    /// <summary>
    /// Optional current properties after the declared CVRAW/CVCIE pixel payload.
    /// Updating one kind replaces its value, preserves other kinds, and truncates the old tail.
    /// The pixel payload and existing container version are never rewritten.
    /// </summary>
    public static class CVFileMetadata
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CVMD0001");
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private const int FooterLength = 64;
        public const int MaximumMetadataBytes = 64 * 1024 * 1024;

        public static IReadOnlyDictionary<string, CVFileProperty> Read(string filePath)
        {
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long pixelEnd = GetPixelEnd(stream);
                return ReadProperties(stream, pixelEnd, false);
            }
        }

        public static void SetProperty(string filePath, string kind, uint version, byte[] value)
        {
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Property kind is required.", nameof(kind));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length > MaximumMetadataBytes) throw new ArgumentOutOfRangeException(nameof(value));
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                long pixelEnd = GetPixelEnd(stream);
                Dictionary<string, CVFileProperty> properties = ReadProperties(stream, pixelEnd, true);
                properties[kind] = new CVFileProperty(version, value);
                byte[] tail = Serialize(properties, pixelEnd);
                stream.Position = pixelEnd;
                byte[] previous = ReadExactly(stream, checked((int)(stream.Length - pixelEnd)));
                try
                {
                    WriteTail(stream, pixelEnd, tail);
                }
                catch (Exception writeError)
                {
                    // Best-effort rollback for a reported write error. A process/power interruption
                    // is detected by the footer/checksum on the next read, not claimed to be atomic.
                    try { WriteTail(stream, pixelEnd, previous); }
                    catch (Exception rollbackError) { throw new AggregateException("Metadata update and rollback failed; pixel data was not rewritten.", writeError, rollbackError); }
                    throw;
                }
            }
        }

        private static void WriteTail(FileStream stream, long pixelEnd, byte[] tail)
        {
            stream.Position = pixelEnd;
            stream.Write(tail, 0, tail.Length);
            stream.SetLength(checked(pixelEnd + tail.Length));
            stream.Flush(true);
        }

        private static Dictionary<string, CVFileProperty> ReadProperties(FileStream stream, long pixelEnd, bool forUpdate)
        {
            var result = new Dictionary<string, CVFileProperty>(StringComparer.Ordinal);
            if (stream.Length == pixelEnd) return result;
            long tailLength = stream.Length - pixelEnd;
            if (tailLength < FooterLength || tailLength > MaximumMetadataBytes + FooterLength)
                throw new InvalidDataException("Invalid CV metadata length.");
            stream.Position = stream.Length - FooterLength;
            byte[] footer = ReadExactly(stream, FooterLength);
            if (!footer.Take(Magic.Length).SequenceEqual(Magic))
                throw new InvalidDataException(forUpdate ? "Unrecognized trailing data; refusing to overwrite it." : "Invalid or incomplete CV metadata footer.");
            using (var reader = new BinaryReader(new MemoryStream(footer), Utf8))
            {
                reader.BaseStream.Position = 8;
                if (reader.ReadUInt32() != 1) throw new NotSupportedException("Unsupported CV metadata container version.");
                uint count = reader.ReadUInt32();
                if (reader.ReadInt64() != pixelEnd) throw new InvalidDataException("Metadata does not match the pixel payload.");
                long bodyLength = reader.ReadInt64();
                if (bodyLength != tailLength - FooterLength || count > bodyLength / 12) throw new InvalidDataException("Invalid CV metadata layout.");
                byte[] expectedHash = reader.ReadBytes(32);
                stream.Position = pixelEnd;
                byte[] body = ReadExactly(stream, checked((int)bodyLength));
                using (SHA256 sha = SHA256.Create())
                    if (!sha.ComputeHash(body).SequenceEqual(expectedHash)) throw new InvalidDataException("CV metadata checksum mismatch.");
                using (var entries = new BinaryReader(new MemoryStream(body), Utf8))
                {
                    for (uint i = 0; i < count; i++)
                    {
                        int keyLength = entries.ReadInt32();
                        if (keyLength <= 0 || keyLength > 4096 || keyLength > entries.BaseStream.Length - entries.BaseStream.Position)
                            throw new InvalidDataException("Invalid metadata property kind.");
                        string key = Utf8.GetString(entries.ReadBytes(keyLength));
                        uint version = entries.ReadUInt32();
                        int length = entries.ReadInt32();
                        if (length < 0 || length > entries.BaseStream.Length - entries.BaseStream.Position || result.ContainsKey(key))
                            throw new InvalidDataException("Invalid or duplicate metadata property.");
                        result.Add(key, new CVFileProperty(version, entries.ReadBytes(length)));
                    }
                    if (entries.BaseStream.Position != body.Length) throw new InvalidDataException("Unexpected metadata bytes.");
                }
            }
            return result;
        }

        private static byte[] Serialize(Dictionary<string, CVFileProperty> properties, long pixelEnd)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, Utf8, true))
            {
                foreach (var entry in properties.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    byte[] kind = Utf8.GetBytes(entry.Key);
                    if (kind.Length == 0 || kind.Length > 4096) throw new ArgumentException("Metadata kind is too long.");
                    writer.Write(kind.Length);
                    writer.Write(kind);
                    writer.Write(entry.Value.Version);
                    writer.Write(entry.Value.Value.Length);
                    writer.Write(entry.Value.Value);
                    if (memory.Length > MaximumMetadataBytes) throw new InvalidDataException("CV metadata exceeds the supported size.");
                }
                long bodyLength = memory.Length;
                byte[] hash;
                using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(memory.ToArray());
                writer.Write(Magic);
                writer.Write((uint)1);
                writer.Write((uint)properties.Count);
                writer.Write(pixelEnd);
                writer.Write(bodyLength);
                writer.Write(hash);
                return memory.ToArray();
            }
        }

        private static long GetPixelEnd(FileStream stream)
        {
            stream.Position = 0;
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(5)) != "CVCIE") throw new InvalidDataException("Not a ColorVision image.");
                uint version = reader.ReadUInt32();
                if (version < 1 || version > 3) throw new NotSupportedException("Unsupported ColorVision image version.");
                int nameLength = reader.ReadInt32();
                if (nameLength < 0 || nameLength > stream.Length - stream.Position) throw new InvalidDataException("Invalid source file name.");
                stream.Position += nameLength;
                if (version == 3) reader.ReadInt32(); // ND port
                reader.ReadSingle(); // gain
                int channels = reader.ReadInt32();
                if (channels <= 0 || channels > 16) throw new InvalidDataException("Invalid channel count.");
                for (int i = 0; i < channels; i++) reader.ReadSingle();
                int width = reader.ReadInt32(), height = reader.ReadInt32(), bpp = reader.ReadInt32();
                long length = version == 2 ? reader.ReadInt64() : reader.ReadInt32();
                if (width <= 0 || height <= 0 || (bpp != 8 && bpp != 16 && bpp != 32 && bpp != 64)
                    || length != checked((long)width * height * channels * (bpp / 8)) || length > stream.Length - stream.Position)
                    throw new InvalidDataException("Invalid ColorVision pixel payload.");
                return checked(stream.Position + length);
            }
        }

        private static byte[] ReadExactly(Stream stream, int length)
        {
            byte[] data = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = stream.Read(data, offset, length - offset);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            return data;
        }
    }
}
