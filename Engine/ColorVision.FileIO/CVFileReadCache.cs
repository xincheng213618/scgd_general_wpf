using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorVision.FileIO
{
    /// <summary>A process-wide, single reusable slot for completed CVRAW files.</summary>
    public static class CVFileReadCache
    {
        private static readonly object Sync = new object();
        private const int MetadataReserve = 64 * 1024;
        private static IntPtr buffer;
        private static long capacity;
        private static long length;
        private static string cachedPath;
        private static DateTime lastWriteUtc;
        private static int readers;
        private static long hits;
        private static long misses;
        private static long allocations;

        public static CVFileReadCacheSnapshot GetSnapshot()
        {
            lock (Sync)
                return new CVFileReadCacheSnapshot(cachedPath, capacity, length, readers, hits, misses, allocations);
        }

        /// <summary>Waits for copies to finish, then frees the slot. Files are never deleted.</summary>
        public static long Release()
        {
            lock (Sync)
            {
                while (readers != 0) Monitor.Wait(Sync);
                long released = capacity;
                ForgetFile();
                FreeBuffer();
                return released;
            }
        }

        /// <summary>
        /// Returns a read-only file snapshot. Header-only callers should not populate on a miss.
        /// Each cached reader holds a real read handle, retaining the existing file-sharing contract.
        /// A busy slot is bypassed rather than allocating another full-sized image.
        /// </summary>
        public static Stream OpenRead(string filePath, bool populateCache = true)
        {
            if (!IsRaw(filePath)) return OpenFile(filePath);
            string path = Path.GetFullPath(filePath);
            lock (Sync)
            {
                FileStream file = OpenFile(path);
                try
                {
                    if (Matches(path, file))
                    {
                        hits++;
                        return Borrow(file);
                    }

                    misses++;
                    if (!populateCache || readers != 0 || file.Length <= 0 || file.Length > int.MaxValue)
                        return file;

                    ForgetFile();
                    try
                    {
                        EnsureCapacity(file.Length);
                        using (Stream destination = OpenBuffer(file.Length, FileAccess.Write))
                            file.CopyTo(destination);
                        Remember(path, file.Length);
                        return Borrow(file);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        Debug.WriteLine("CVRAW cache unavailable: " + ex.Message);
                        file.Position = 0;
                        return file;
                    }
                }
                catch
                {
                    file.Dispose();
                    throw;
                }
            }
        }

        // Disk and cache receive the same bytes; publish the key only after the file closes.
        internal static bool WriteFile(string path, long fileLength, Action<Stream> write)
        {
            if (!IsRaw(path))
            {
                using (Stream file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) write(file);
                return true;
            }
            lock (Sync)
            {
                while (readers != 0) Monitor.Wait(Sync);
                ForgetFile();
                bool cacheReady = false;
                if (fileLength > 0 && fileLength <= int.MaxValue)
                {
                    try { EnsureCapacity(fileLength); cacheReady = true; }
                    catch (OutOfMemoryException ex) { Debug.WriteLine("CVRAW saved without cache: " + ex.Message); }
                }
                using (Stream file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (cacheReady)
                    {
                        using (var destination = new CacheWritingStream(file, fileLength))
                        {
                            write(destination);
                            cacheReady = destination.IsComplete;
                        }
                    }
                    else write(file);
                }
                if (cacheReady) Remember(Path.GetFullPath(path), fileLength);
                return true;
            }
        }

        internal static void UpdateMetadata(string path, Func<FileTail> update)
        {
            if (!IsRaw(path)) { update(); return; }
            lock (Sync)
            {
                while (readers != 0) Monitor.Wait(Sync);
                bool preserve = false;
                if (SamePath(path))
                {
                    using (FileStream file = OpenFile(path)) preserve = Matches(Path.GetFullPath(path), file);
                    if (!preserve) ForgetFile();
                }
                try
                {
                    FileTail tail = update();
                    if (!preserve) return;
                    try
                    {
                        long newLength = checked(tail.Offset + tail.Bytes.Length);
                        EnsureCapacity(newLength);
                        using (Stream destination = OpenBuffer(newLength, FileAccess.Write))
                        {
                            destination.Position = tail.Offset;
                            destination.Write(tail.Bytes, 0, tail.Bytes.Length);
                        }
                        Remember(Path.GetFullPath(path), newLength);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        ForgetFile();
                        Debug.WriteLine("CVRAW metadata saved without cache: " + ex.Message);
                    }
                }
                catch
                {
                    if (SamePath(path)) ForgetFile();
                    throw;
                }
            }
        }

        private static bool IsRaw(string path) => string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase);
        private static FileStream OpenFile(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        private static bool SamePath(string path) => cachedPath != null && string.Equals(cachedPath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        private static bool Matches(string path, FileStream file)
            => SamePath(path) && length == file.Length && lastWriteUtc == File.GetLastWriteTimeUtc(path);

        private static void Remember(string path, long fileLength)
        {
            cachedPath = path;
            length = fileLength;
            lastWriteUtc = File.GetLastWriteTimeUtc(path);
        }

        private static void ForgetFile() { cachedPath = null; length = 0; }

        private static unsafe void EnsureCapacity(long required)
        {
            if (required <= capacity) return;
            long replacementCapacity = checked(required + MetadataReserve);
            if (IntPtr.Size == 4)
            {
                replacementCapacity = Math.Min(replacementCapacity, int.MaxValue);
                if (required > replacementCapacity) throw new OutOfMemoryException("CVRAW cache exceeds the process address limit.");
            }
            IntPtr replacement = Marshal.AllocHGlobal(new IntPtr(replacementCapacity));
            if (buffer != IntPtr.Zero && length > 0)
                Buffer.MemoryCopy(buffer.ToPointer(), replacement.ToPointer(), replacementCapacity, length);
            FreeBuffer();
            buffer = replacement;
            capacity = replacementCapacity;
            allocations++;
            GC.AddMemoryPressure(capacity);
        }

        private static void FreeBuffer()
        {
            if (buffer == IntPtr.Zero) return;
            Marshal.FreeHGlobal(buffer);
            GC.RemoveMemoryPressure(capacity);
            buffer = IntPtr.Zero;
            capacity = 0;
        }

        private static unsafe Stream OpenBuffer(long size, FileAccess access)
            => new UnmanagedMemoryStream((byte*)buffer.ToPointer(), size, capacity, access);

        private static Stream Borrow(FileStream file)
        {
            Stream memory = OpenBuffer(length, FileAccess.Read);
            readers++;
            return new CachedReadStream(memory, file);
        }

        internal sealed class FileTail
        {
            internal FileTail(long offset, byte[] bytes) { Offset = offset; Bytes = bytes; }
            internal long Offset { get; }
            internal byte[] Bytes { get; }
        }

        private sealed class CacheWritingStream : Stream
        {
            private readonly Stream file;
            private readonly Stream memory;
            private bool sequential = true;
            internal CacheWritingStream(Stream file, long size) { this.file = file; memory = OpenBuffer(size, FileAccess.Write); }
            internal bool IsComplete => sequential && file.Length == memory.Length && memory.Position == memory.Length;
            public override bool CanRead => false;
            public override bool CanSeek => file.CanSeek;
            public override bool CanWrite => file.CanWrite;
            public override long Length => file.Length;
            public override long Position
            {
                get => file.Position;
                set { if (value != file.Position) sequential = false; file.Position = value; }
            }
            public override void Write(byte[] source, int offset, int count)
            {
                if (!sequential) { file.Write(source, offset, count); return; }
                memory.Position = file.Position;
                if (count > memory.Length - memory.Position) throw new InvalidDataException("CVRAW writer exceeded its declared length.");
                file.Write(source, offset, count);
                memory.Write(source, offset, count);
            }
#if NETCOREAPP
            public override void Write(ReadOnlySpan<byte> source)
            {
                if (!sequential) { file.Write(source); return; }
                memory.Position = file.Position;
                if (source.Length > memory.Length - memory.Position) throw new InvalidDataException("CVRAW writer exceeded its declared length.");
                file.Write(source);
                memory.Write(source);
            }
#endif
            public override long Seek(long offset, SeekOrigin origin)
            {
                long previous = file.Position;
                long position = file.Seek(offset, origin);
                if (position != previous) sequential = false;
                return position;
            }
            public override void Flush() => file.Flush();
            public override void SetLength(long value) { sequential = false; file.SetLength(value); }
            public override int Read(byte[] destination, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) { memory.Dispose(); file.Dispose(); }
                base.Dispose(disposing);
            }
        }

        // Do not expose UnmanagedMemoryStream.PositionPointer or writable cache memory to consumers.
        private sealed class CachedReadStream : Stream
        {
            private readonly Stream memory;
            private FileStream file;
            internal CachedReadStream(Stream memory, FileStream file) { this.memory = memory; this.file = file; }
            ~CachedReadStream() { Dispose(false); }
            public override bool CanRead => file != null;
            public override bool CanSeek => file != null;
            public override bool CanWrite => false;
            public override long Length => memory.Length;
            public override long Position { get => memory.Position; set => memory.Position = value; }
            public override int Read(byte[] destination, int offset, int count) => memory.Read(destination, offset, count);
#if NETCOREAPP
            public override int Read(Span<byte> destination) => memory.Read(destination);
#endif
            public override int ReadByte() => memory.ReadByte();
            public override long Seek(long offset, SeekOrigin origin) => memory.Seek(offset, origin);
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] source, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                FileStream owned = Interlocked.Exchange(ref file, null);
                if (owned != null)
                {
                    try { memory.Dispose(); owned.Dispose(); }
                    finally { lock (Sync) { readers--; Monitor.PulseAll(Sync); } }
                }
                base.Dispose(disposing);
            }
        }
    }

    public sealed class CVFileReadCacheSnapshot
    {
        internal CVFileReadCacheSnapshot(string path, long capacity, long length, int readers, long hits, long misses, long allocations)
        { FilePath = path; CapacityBytes = capacity; ContentBytes = length; ActiveReaders = readers; HitCount = hits; MissCount = misses; AllocationCount = allocations; }
        public string FilePath { get; }
        public long CapacityBytes { get; }
        public long ContentBytes { get; }
        public int ActiveReaders { get; }
        public long HitCount { get; }
        public long MissCount { get; }
        public long AllocationCount { get; }
    }
}
