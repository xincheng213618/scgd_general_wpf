using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Media
{
    /// <summary>
    /// Adds or replaces the TIFF ImageDescription tag without decoding or re-encoding pixel data.
    /// </summary>
    internal static class TiffImageDescriptionWriter
    {
        private const ushort ClassicTiffVersion = 42;
        private const ushort BigTiffVersion = 43;
        private const ushort ImageDescriptionTag = 270;
        private const ushort AsciiFieldType = 2;

        public static void Write(string filePath, string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(description);

            byte[] descriptionBytes = Encoding.ASCII.GetBytes(description + '\0');
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            TiffLayout layout = ReadLayout(stream);
            TiffDirectory directory = ReadDirectory(stream, layout);

            List<TiffEntry> entries = directory.Entries
                .Where(entry => entry.Tag != ImageDescriptionTag)
                .ToList();
            if (entries.Count == layout.MaximumEntryCount)
                throw new InvalidDataException("The TIFF directory has no room for another entry.");

            ulong descriptionOffset = AppendAligned(stream, descriptionBytes, layout.Alignment);
            byte[] descriptionEntry = CreateDescriptionEntry(layout, descriptionBytes.LongLength, descriptionOffset);
            entries.Add(new TiffEntry(ImageDescriptionTag, descriptionEntry));
            entries.Sort(static (left, right) => left.Tag.CompareTo(right.Tag));

            ulong newDirectoryOffset = AlignOffset(checked((ulong)stream.Length), layout.Alignment);
            PadTo(stream, newDirectoryOffset);
            WriteDirectory(stream, layout, entries, directory.NextDirectoryOffset);
            stream.Flush(flushToDisk: true);

            WriteFirstDirectoryOffset(stream, layout, newDirectoryOffset);
            stream.Flush(flushToDisk: true);
        }

        private static TiffLayout ReadLayout(FileStream stream)
        {
            if (stream.Length < 8)
                throw new InvalidDataException("The file is too short to be a TIFF image.");

            Span<byte> header = stackalloc byte[16];
            stream.Position = 0;
            stream.ReadExactly(header[..8]);
            bool littleEndian = header[0] == (byte)'I' && header[1] == (byte)'I';
            bool bigEndian = header[0] == (byte)'M' && header[1] == (byte)'M';
            if (!littleEndian && !bigEndian)
                throw new InvalidDataException("The TIFF byte-order marker is invalid.");

            ushort version = ReadUInt16(header[2..4], littleEndian);
            if (version == ClassicTiffVersion)
            {
                return new TiffLayout(
                    littleEndian,
                    false,
                    2,
                    12,
                    4,
                    4,
                    ReadUInt32(header[4..8], littleEndian),
                    2,
                    ushort.MaxValue);
            }

            if (version != BigTiffVersion)
                throw new InvalidDataException($"Unsupported TIFF version: {version}.");
            if (stream.Length < 16)
                throw new InvalidDataException("The file is too short to contain a BigTIFF header.");

            stream.Position = 8;
            stream.ReadExactly(header[8..16]);
            ushort offsetSize = ReadUInt16(header[4..6], littleEndian);
            ushort reserved = ReadUInt16(header[6..8], littleEndian);
            if (offsetSize != 8 || reserved != 0)
                throw new InvalidDataException("The BigTIFF offset layout is unsupported.");

            return new TiffLayout(
                littleEndian,
                true,
                8,
                20,
                8,
                8,
                ReadUInt64(header[8..16], littleEndian),
                8,
                int.MaxValue);
        }

        private static TiffDirectory ReadDirectory(FileStream stream, TiffLayout layout)
        {
            EnsureRange(stream, layout.FirstDirectoryOffset, (ulong)layout.EntryCountSize);
            stream.Position = checked((long)layout.FirstDirectoryOffset);

            Span<byte> countBuffer = stackalloc byte[8];
            stream.ReadExactly(countBuffer[..layout.EntryCountSize]);
            ulong entryCount = layout.IsBigTiff
                ? ReadUInt64(countBuffer, layout.LittleEndian)
                : ReadUInt16(countBuffer[..2], layout.LittleEndian);
            if (entryCount > (ulong)layout.MaximumEntryCount || entryCount > int.MaxValue)
                throw new InvalidDataException($"The TIFF directory entry count is unsupported: {entryCount}.");

            ulong entryBytesLength = checked(entryCount * (ulong)layout.EntrySize);
            ulong directoryLength = checked((ulong)layout.EntryCountSize + entryBytesLength + (ulong)layout.OffsetSize);
            EnsureRange(stream, layout.FirstDirectoryOffset, directoryLength);

            List<TiffEntry> entries = new((int)entryCount);
            byte[] entryBuffer = new byte[layout.EntrySize];
            for (ulong index = 0; index < entryCount; index++)
            {
                stream.ReadExactly(entryBuffer);
                byte[] rawEntry = (byte[])entryBuffer.Clone();
                entries.Add(new TiffEntry(ReadUInt16(rawEntry.AsSpan(0, 2), layout.LittleEndian), rawEntry));
            }

            Span<byte> nextOffsetBuffer = stackalloc byte[8];
            stream.ReadExactly(nextOffsetBuffer[..layout.OffsetSize]);
            ulong nextDirectoryOffset = layout.IsBigTiff
                ? ReadUInt64(nextOffsetBuffer, layout.LittleEndian)
                : ReadUInt32(nextOffsetBuffer[..4], layout.LittleEndian);
            return new TiffDirectory(entries, nextDirectoryOffset);
        }

        private static byte[] CreateDescriptionEntry(TiffLayout layout, long byteCount, ulong valueOffset)
        {
            byte[] entry = new byte[layout.EntrySize];
            WriteUInt16(entry.AsSpan(0, 2), ImageDescriptionTag, layout.LittleEndian);
            WriteUInt16(entry.AsSpan(2, 2), AsciiFieldType, layout.LittleEndian);
            if (layout.IsBigTiff)
            {
                WriteUInt64(entry.AsSpan(4, 8), checked((ulong)byteCount), layout.LittleEndian);
                WriteUInt64(entry.AsSpan(12, 8), valueOffset, layout.LittleEndian);
            }
            else
            {
                WriteUInt32(entry.AsSpan(4, 4), checked((uint)byteCount), layout.LittleEndian);
                WriteUInt32(entry.AsSpan(8, 4), checked((uint)valueOffset), layout.LittleEndian);
            }
            return entry;
        }

        private static ulong AppendAligned(FileStream stream, byte[] data, int alignment)
        {
            ulong offset = AlignOffset(checked((ulong)stream.Length), alignment);
            PadTo(stream, offset);
            stream.Write(data);
            return offset;
        }

        private static void WriteDirectory(
            FileStream stream,
            TiffLayout layout,
            IReadOnlyList<TiffEntry> entries,
            ulong nextDirectoryOffset)
        {
            Span<byte> buffer = stackalloc byte[8];
            if (layout.IsBigTiff)
            {
                WriteUInt64(buffer, checked((ulong)entries.Count), layout.LittleEndian);
                stream.Write(buffer);
            }
            else
            {
                WriteUInt16(buffer[..2], checked((ushort)entries.Count), layout.LittleEndian);
                stream.Write(buffer[..2]);
            }

            foreach (TiffEntry entry in entries)
                stream.Write(entry.RawEntry);

            if (layout.IsBigTiff)
            {
                WriteUInt64(buffer, nextDirectoryOffset, layout.LittleEndian);
                stream.Write(buffer);
            }
            else
            {
                WriteUInt32(buffer[..4], checked((uint)nextDirectoryOffset), layout.LittleEndian);
                stream.Write(buffer[..4]);
            }
        }

        private static void WriteFirstDirectoryOffset(FileStream stream, TiffLayout layout, ulong directoryOffset)
        {
            Span<byte> buffer = stackalloc byte[8];
            stream.Position = layout.FirstDirectoryOffsetPosition;
            if (layout.IsBigTiff)
            {
                WriteUInt64(buffer, directoryOffset, layout.LittleEndian);
                stream.Write(buffer);
            }
            else
            {
                WriteUInt32(buffer[..4], checked((uint)directoryOffset), layout.LittleEndian);
                stream.Write(buffer[..4]);
            }
        }

        private static ulong AlignOffset(ulong value, int alignment)
        {
            ulong mask = checked((ulong)alignment - 1);
            return checked((value + mask) & ~mask);
        }

        private static void PadTo(FileStream stream, ulong offset)
        {
            if (offset > long.MaxValue)
                throw new IOException("The TIFF output offset exceeds the supported file size.");
            stream.Position = stream.Length;
            while ((ulong)stream.Position < offset)
                stream.WriteByte(0);
        }

        private static void EnsureRange(FileStream stream, ulong offset, ulong length)
        {
            ulong fileLength = checked((ulong)stream.Length);
            if (offset > fileLength || length > fileLength - offset || offset > long.MaxValue)
                throw new InvalidDataException("The TIFF directory points outside the file.");
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> value, bool littleEndian) => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(value)
            : BinaryPrimitives.ReadUInt16BigEndian(value);

        private static uint ReadUInt32(ReadOnlySpan<byte> value, bool littleEndian) => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(value)
            : BinaryPrimitives.ReadUInt32BigEndian(value);

        private static ulong ReadUInt64(ReadOnlySpan<byte> value, bool littleEndian) => littleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(value)
            : BinaryPrimitives.ReadUInt64BigEndian(value);

        private static void WriteUInt16(Span<byte> destination, ushort value, bool littleEndian)
        {
            if (littleEndian) BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
            else BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        }

        private static void WriteUInt32(Span<byte> destination, uint value, bool littleEndian)
        {
            if (littleEndian) BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
            else BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        }

        private static void WriteUInt64(Span<byte> destination, ulong value, bool littleEndian)
        {
            if (littleEndian) BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
            else BinaryPrimitives.WriteUInt64BigEndian(destination, value);
        }

        private sealed record TiffEntry(ushort Tag, byte[] RawEntry);

        private sealed record TiffDirectory(IReadOnlyList<TiffEntry> Entries, ulong NextDirectoryOffset);

        private sealed record TiffLayout(
            bool LittleEndian,
            bool IsBigTiff,
            int EntryCountSize,
            int EntrySize,
            int OffsetSize,
            int FirstDirectoryOffsetPosition,
            ulong FirstDirectoryOffset,
            int Alignment,
            int MaximumEntryCount);
    }
}
