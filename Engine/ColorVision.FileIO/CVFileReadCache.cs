using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorVision.FileIO
{
    /// <summary>A process-wide, bounded LRU cache of completed CVRAW files.</summary>
    public static class CVFileReadCache
    {
        private static readonly object Sync = new object();
        private static readonly List<CacheEntry> Entries = new List<CacheEntry>();
        private const int MetadataReserve = 64 * 1024;
        private static long hits;
        private static long misses;
        private static long allocations;
        private static long accessSequence;
        private static bool isEnabled = true;
        private static int maximumEntries = 1;

        /// <summary>Disabling bypasses the cache; borrowed entries are freed when their final reader returns.</summary>
        public static bool IsEnabled
        {
            get { lock (Sync) return isEnabled; }
            set
            {
                lock (Sync)
                {
                    isEnabled = value;
                    if (!value)
                    {
                        foreach (CacheEntry entry in Entries.ToArray()) Retire(entry);
                    }
                }
            }
        }

        /// <summary>Maximum resident file slots. Borrowed surplus entries are retired safely after a reduction.</summary>
        public static int MaximumEntries
        {
            get { lock (Sync) return maximumEntries; }
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The cache entry limit must be positive.");
                lock (Sync)
                {
                    maximumEntries = value;
                    int retainedCount = 0;
                    foreach (CacheEntry entry in Entries) if (!entry.IsRetired) retainedCount++;
                    while (retainedCount > maximumEntries)
                    {
                        CacheEntry surplus = FindOldest(requireIdle: true) ?? FindOldest(requireIdle: false);
                        Retire(surplus);
                        retainedCount--;
                    }
                }
            }
        }

        public static CVFileReadCacheSnapshot GetSnapshot()
        {
            lock (Sync)
            {
                List<CacheEntry> ordered = new List<CacheEntry>(Entries);
                ordered.Sort((left, right) => right.LastAccessSequence.CompareTo(left.LastAccessSequence));
                List<CVFileReadCacheEntrySnapshot> snapshots = new List<CVFileReadCacheEntrySnapshot>();
                long capacity = 0;
                long content = 0;
                int readers = 0;
                string latestPath = null;
                foreach (CacheEntry entry in ordered)
                {
                    capacity = checked(capacity + entry.Capacity);
                    content = checked(content + entry.Length);
                    readers = checked(readers + entry.Readers);
                    if (latestPath == null && !entry.IsRetired) latestPath = entry.FilePath;
                    snapshots.Add(new CVFileReadCacheEntrySnapshot(entry.FilePath, entry.Capacity, entry.Length, entry.Readers,
                        entry.HitCount, entry.AllocationCount, entry.IsRetired));
                }
                return new CVFileReadCacheSnapshot(latestPath, capacity, content, readers, hits, misses, allocations,
                    isEnabled, maximumEntries, snapshots.AsReadOnly());
            }
        }

        /// <summary>Waits for borrowed copies to finish, then frees every cache slot. Files are never deleted.</summary>
        public static long Release()
        {
            lock (Sync)
            {
                while (HasReaders()) Monitor.Wait(Sync);
                long released = 0;
                foreach (CacheEntry entry in Entries)
                {
                    released = checked(released + entry.Capacity);
                    FreeBuffer(entry);
                }
                Entries.Clear();
                return released;
            }
        }

        /// <summary>
        /// Returns a read-only file snapshot. Header-only misses do not populate the cache.
        /// Each cached reader holds a real read handle, retaining the existing file-sharing contract.
        /// When every slot is borrowed, a miss is served from disk without an additional allocation.
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
                    if (!isEnabled) return file;
                    CacheEntry existing = FindEntry(path);
                    if (existing != null && Matches(existing, path, file))
                    {
                        hits++;
                        existing.HitCount++;
                        return Borrow(existing, file);
                    }

                    misses++;
                    if (existing != null && existing.Readers != 0)
                    {
                        Retire(existing);
                        existing = null;
                    }
                    if (!populateCache || file.Length <= 0 || file.Length > int.MaxValue) return file;
                    CacheEntry entry = existing ?? GetWritableEntry();
                    if (entry == null) return file;
                    ForgetFile(entry);
                    try
                    {
                        EnsureCapacity(entry, file.Length);
                        using (Stream destination = OpenBuffer(entry, file.Length, FileAccess.Write)) file.CopyTo(destination);
                        Remember(entry, path, file.Length);
                        return Borrow(entry, file);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        if (entry.Capacity == 0) Entries.Remove(entry);
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
            string fullPath = Path.GetFullPath(path);
            lock (Sync)
            {
                while (HasReaders(fullPath)) Monitor.Wait(Sync);
                CacheEntry entry = FindEntry(fullPath);
                if (entry != null) ForgetFile(entry);
                bool cacheReady = false;
                if (isEnabled && fileLength > 0 && fileLength <= int.MaxValue)
                {
                    entry = entry ?? GetWritableEntry();
                    if (entry != null)
                    {
                        ForgetFile(entry);
                        try { EnsureCapacity(entry, fileLength); cacheReady = true; }
                        catch (OutOfMemoryException ex)
                        {
                            if (entry.Capacity == 0) Entries.Remove(entry);
                            Debug.WriteLine("CVRAW saved without cache: " + ex.Message);
                        }
                    }
                }
                using (Stream file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (cacheReady)
                    {
                        using (var destination = new CacheWritingStream(entry, file, fileLength))
                        {
                            write(destination);
                            cacheReady = destination.IsComplete;
                        }
                    }
                    else write(file);
                }
                if (cacheReady) Remember(entry, fullPath, fileLength);
                return true;
            }
        }

        internal static void UpdateMetadata(string path, Func<FileTail> update)
        {
            if (!IsRaw(path)) { update(); return; }
            string fullPath = Path.GetFullPath(path);
            lock (Sync)
            {
                while (HasReaders(fullPath)) Monitor.Wait(Sync);
                CacheEntry entry = FindEntry(fullPath);
                bool preserve = false;
                if (isEnabled && entry != null)
                {
                    using (FileStream file = OpenFile(path)) preserve = Matches(entry, fullPath, file);
                    if (!preserve) ForgetFile(entry);
                }
                try
                {
                    FileTail tail = update();
                    if (!preserve) return;
                    try
                    {
                        long newLength = checked(tail.Offset + tail.Bytes.Length);
                        EnsureCapacity(entry, newLength);
                        using (Stream destination = OpenBuffer(entry, newLength, FileAccess.Write))
                        {
                            destination.Position = tail.Offset;
                            destination.Write(tail.Bytes, 0, tail.Bytes.Length);
                        }
                        Remember(entry, fullPath, newLength);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        ForgetFile(entry);
                        Debug.WriteLine("CVRAW metadata saved without cache: " + ex.Message);
                    }
                }
                catch
                {
                    if (entry != null) ForgetFile(entry);
                    throw;
                }
            }
        }

        private static bool IsRaw(string path) => string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase);
        private static FileStream OpenFile(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        private static CacheEntry FindEntry(string path)
            => Entries.Find(entry => !entry.IsRetired && string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase));

        private static CacheEntry FindOldest(bool requireIdle)
        {
            CacheEntry oldest = null;
            foreach (CacheEntry entry in Entries)
            {
                if (entry.IsRetired || (requireIdle && entry.Readers != 0)) continue;
                if (oldest == null || entry.LastAccessSequence < oldest.LastAccessSequence) oldest = entry;
            }
            return oldest;
        }

        private static CacheEntry GetWritableEntry()
        {
            // Retired entries still occupy a slot until their borrowed memory is returned.
            if (Entries.Count < maximumEntries)
            {
                CacheEntry entry = new CacheEntry();
                Entries.Add(entry);
                return entry;
            }
            return FindOldest(requireIdle: true);
        }

        private static bool HasReaders(string path = null)
        {
            foreach (CacheEntry entry in Entries)
            {
                if (entry.Readers != 0 && (path == null || string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase))) return true;
            }
            return false;
        }

        private static bool Matches(CacheEntry entry, string path, FileStream file)
            => entry.Length == file.Length && entry.LastWriteUtc == File.GetLastWriteTimeUtc(path);

        private static void Remember(CacheEntry entry, string path, long fileLength)
        {
            entry.FilePath = path;
            entry.Length = fileLength;
            entry.LastWriteUtc = File.GetLastWriteTimeUtc(path);
            entry.LastAccessSequence = ++accessSequence;
        }

        private static void ForgetFile(CacheEntry entry)
        {
            entry.FilePath = null;
            entry.Length = 0;
            entry.HitCount = 0;
            entry.LastAccessSequence = ++accessSequence;
        }

        private static void Retire(CacheEntry entry)
        {
            entry.IsRetired = true;
            if (entry.Readers == 0)
            {
                FreeBuffer(entry);
                Entries.Remove(entry);
            }
        }

        private static unsafe void EnsureCapacity(CacheEntry entry, long required)
        {
            if (required <= entry.Capacity) return;
            long replacementCapacity = checked(required + MetadataReserve);
            if (IntPtr.Size == 4)
            {
                replacementCapacity = Math.Min(replacementCapacity, int.MaxValue);
                if (required > replacementCapacity) throw new OutOfMemoryException("CVRAW cache exceeds the process address limit.");
            }
            IntPtr replacement = Marshal.AllocHGlobal(new IntPtr(replacementCapacity));
            if (entry.Buffer != IntPtr.Zero && entry.Length > 0)
                Buffer.MemoryCopy(entry.Buffer.ToPointer(), replacement.ToPointer(), replacementCapacity, entry.Length);
            FreeBuffer(entry);
            entry.Buffer = replacement;
            entry.Capacity = replacementCapacity;
            entry.AllocationCount++;
            allocations++;
            GC.AddMemoryPressure(entry.Capacity);
        }

        private static void FreeBuffer(CacheEntry entry)
        {
            if (entry.Buffer == IntPtr.Zero) return;
            Marshal.FreeHGlobal(entry.Buffer);
            GC.RemoveMemoryPressure(entry.Capacity);
            entry.Buffer = IntPtr.Zero;
            entry.Capacity = 0;
        }

        private static unsafe Stream OpenBuffer(CacheEntry entry, long size, FileAccess access)
            => new UnmanagedMemoryStream((byte*)entry.Buffer.ToPointer(), size, entry.Capacity, access);

        private static Stream Borrow(CacheEntry entry, FileStream file)
        {
            Stream memory = OpenBuffer(entry, entry.Length, FileAccess.Read);
            entry.Readers++;
            entry.LastAccessSequence = ++accessSequence;
            return new CachedReadStream(entry, memory, file);
        }

        private sealed class CacheEntry
        {
            internal IntPtr Buffer;
            internal long Capacity;
            internal long Length;
            internal string FilePath;
            internal DateTime LastWriteUtc;
            internal int Readers;
            internal long HitCount;
            internal long AllocationCount;
            internal long LastAccessSequence;
            internal bool IsRetired;
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
            internal CacheWritingStream(CacheEntry entry, Stream file, long size) { this.file = file; memory = OpenBuffer(entry, size, FileAccess.Write); }
            internal bool IsComplete => sequential && file.Length == memory.Length && memory.Position == memory.Length;
            public override bool CanRead => false;
            public override bool CanSeek => file.CanSeek;
            public override bool CanWrite => true;
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
            private readonly CacheEntry entry;
            private readonly Stream memory;
            private FileStream file;
            internal CachedReadStream(CacheEntry entry, Stream memory, FileStream file) { this.entry = entry; this.memory = memory; this.file = file; }
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
                    finally
                    {
                        lock (Sync)
                        {
                            entry.Readers--;
                            if (entry.IsRetired && entry.Readers == 0)
                            {
                                FreeBuffer(entry);
                                Entries.Remove(entry);
                            }
                            Monitor.PulseAll(Sync);
                        }
                    }
                }
                base.Dispose(disposing);
            }
        }
    }

    public sealed class CVFileReadCacheSnapshot
    {
        internal CVFileReadCacheSnapshot(string path, long capacity, long length, int readers, long hits, long misses, long allocations,
            bool isEnabled, int maximumEntries, IReadOnlyList<CVFileReadCacheEntrySnapshot> entries)
        {
            FilePath = path; CapacityBytes = capacity; ContentBytes = length; ActiveReaders = readers; HitCount = hits; MissCount = misses;
            AllocationCount = allocations; IsEnabled = isEnabled; MaximumEntries = maximumEntries; Entries = entries;
        }
        public bool IsEnabled { get; }
        public int MaximumEntries { get; }
        public IReadOnlyList<CVFileReadCacheEntrySnapshot> Entries { get; }
        public string FilePath { get; }
        public long CapacityBytes { get; }
        public long ContentBytes { get; }
        public int ActiveReaders { get; }
        public long HitCount { get; }
        public long MissCount { get; }
        public long AllocationCount { get; }
    }

    public sealed class CVFileReadCacheEntrySnapshot
    {
        internal CVFileReadCacheEntrySnapshot(string path, long capacity, long length, int readers, long hits, long allocations, bool isRetired)
        {
            FilePath = path; CapacityBytes = capacity; ContentBytes = length; ActiveReaders = readers;
            HitCount = hits; AllocationCount = allocations; IsRetired = isRetired;
        }
        public string FilePath { get; }
        public long CapacityBytes { get; }
        public long ContentBytes { get; }
        public int ActiveReaders { get; }
        public long HitCount { get; }
        public long AllocationCount { get; }
        public bool IsRetired { get; }
    }
}
