using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private static readonly object WriteSync = new object();

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
                foreach (CacheEntry entry in Entries.ToArray())
                {
                    if (entry.Writers == 0) released = checked(released + entry.Capacity);
                    Retire(entry);
                }
                return released;
            }
        }

        /// <summary>
        /// Returns a read-only file snapshot. Header-only misses do not populate the cache.
        /// A cache hit reads memory directly without opening or checking the disk file.
        /// When every slot is borrowed, a miss is served from disk without an additional allocation.
        /// </summary>
        public static Stream OpenRead(string filePath, bool populateCache = true)
        {
            if (!IsRaw(filePath)) return OpenFile(filePath);
            string path = Path.GetFullPath(filePath);
            return OpenReadCore(path, populateCache, () => OpenFile(path));
        }

        private static Stream OpenReadCore(string path, bool populateCache, Func<Stream> openFile)
        {
            CacheEntry entry = null;
            lock (Sync)
            {
                CacheEntry existing = isEnabled ? FindEntry(path) : null;
                if (existing != null)
                {
                    hits++;
                    existing.HitCount++;
                    return Borrow(existing);
                }
                if (isEnabled)
                {
                    misses++;
                    entry = populateCache ? GetWritableEntry() : null;
                    if (entry != null)
                    {
                        ForgetFile(entry);
                        entry.FilePath = path;
                        entry.IsLoading = true;
                        entry.Readers++; // Pin the buffer while disk I/O runs outside Sync.
                    }
                }
            }

            Stream file = null;
            bool completed = false;
            try
            {
                file = openFile();
                if (entry == null) return file;
                long length = file.Length;
                if (length <= 0 || length > int.MaxValue) return file;
                lock (Sync) EnsureCapacity(entry, length);
                using (Stream destination = OpenBuffer(entry, length, FileAccess.Write))
                {
                    file.CopyTo(destination);
                    if (destination.Position != length) throw new EndOfStreamException("Incomplete CVRAW cache snapshot.");
                }
                file.Dispose();
                file = null;
                lock (Sync)
                {
                    entry.Length = length;
                    if (!entry.IsRetired && FindEntry(path) == null) Remember(entry, path, length);
                    else Retire(entry);
                    Stream result = Borrow(entry);
                    completed = true;
                    return result;
                }
            }
            catch (OutOfMemoryException ex) when (file != null)
            {
                Debug.WriteLine("CVRAW cache unavailable: " + ex.Message);
                file.Position = 0;
                return file;
            }
            catch
            {
                file?.Dispose();
                throw;
            }
            finally
            {
                if (entry != null)
                {
                    lock (Sync)
                    {
                        entry.IsLoading = false;
                        entry.Readers--;
                        if (!completed || entry.IsRetired) Retire(entry);
                        Monitor.PulseAll(Sync);
                    }
                }
            }
        }

        /// <summary>Returns the cached length without accessing the disk file, or null on a miss.</summary>
        public static long? GetCachedLength(string filePath)
        {
            if (!IsRaw(filePath)) return null;
            lock (Sync) return isEnabled ? FindEntry(Path.GetFullPath(filePath))?.Length : null;
        }

        // Save on the calling thread; disk I/O never holds the cache lookup lock.
        internal static bool WriteFile(string path, long fileLength, Action<Stream> write, CVFileSaveMode saveMode = CVFileSaveMode.Synchronous)
        {
            // Previously compiled callers may still pass the retired asynchronous value.
            if ((int)saveMode == 1) saveMode = CVFileSaveMode.Synchronous;
            if (saveMode != CVFileSaveMode.Synchronous && saveMode != CVFileSaveMode.MemoryOnly)
                throw new ArgumentOutOfRangeException(nameof(saveMode));
            if (!IsRaw(path))
            {
                if (saveMode != CVFileSaveMode.Synchronous) throw new NotSupportedException("Memory-only saving requires CVRAW.");
                using (Stream file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) write(file);
                return true;
            }
            string fullPath = Path.GetFullPath(path);
            lock (WriteSync)
            {
                CacheEntry entry;
                lock (Sync)
                {
                    while (HasFileLoad(fullPath)) Monitor.Wait(Sync);
                    if (saveMode == CVFileSaveMode.MemoryOnly && !isEnabled)
                        throw new InvalidOperationException("Memory-only saving requires the CVRAW cache.");
                    entry = FindEntry(fullPath);
                    if (entry != null && entry.Readers != 0) { Retire(entry); entry = null; }
                    if (isEnabled && fileLength > 0 && fileLength <= int.MaxValue)
                    {
                        entry = entry ?? GetWritableEntry();
                        if (entry == null && saveMode == CVFileSaveMode.MemoryOnly)
                        {
                            // Readers may still own the previous snapshot after its cache key is retired.
                            int activeEntries = 0;
                            foreach (CacheEntry candidate in Entries) if (!candidate.IsRetired) activeEntries++;
                            if (activeEntries >= maximumEntries) Retire(FindOldest(requireIdle: false));
                            entry = new CacheEntry();
                            Entries.Add(entry);
                        }
                        if (entry != null)
                        {
                            ForgetFile(entry);
                            try { EnsureCapacity(entry, fileLength); }
                            catch (OutOfMemoryException) when (saveMode == CVFileSaveMode.Synchronous)
                            {
                                Retire(entry);
                                entry = null;
                            }
                        }
                    }
                    else entry = null;
                    if (entry != null)
                    {
                        try
                        {
                            using (Stream memory = OpenBuffer(entry, fileLength, FileAccess.Write))
                            {
                                write(memory);
                                if (memory.Position != fileLength) throw new InvalidDataException("CVRAW writer did not write its declared length.");
                            }
                            entry.SaveMode = saveMode;
                            Remember(entry, fullPath, fileLength);
                            if (saveMode == CVFileSaveMode.MemoryOnly) return true;
                            entry.Writers++; // Pin the buffer until this synchronous disk write returns.
                        }
                        catch { Retire(entry); throw; }
                    }
                    else if (saveMode == CVFileSaveMode.MemoryOnly)
                        throw new InvalidOperationException("The CVRAW snapshot could not be cached.");
                }
                try
                {
                    using (Stream file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        if (entry == null) write(file);
                        else using (Stream memory = OpenBuffer(entry, fileLength, FileAccess.Read)) memory.CopyTo(file);
                    }
                }
                finally
                {
                    if (entry != null)
                    {
                        lock (Sync)
                        {
                            entry.Writers--;
                            if (entry.IsRetired && entry.Readers == 0) Retire(entry);
                            Monitor.PulseAll(Sync);
                        }
                    }
                }
            }
            return true;
        }

        // Publish a complete metadata tail; existing readers retain their previous tail.
        internal static void UpdateMetadata(string path, Func<Stream, FileTail> update)
        {
            string fullPath = Path.GetFullPath(path);
            lock (WriteSync)
            {
                FileTail tail = null;
                lock (Sync)
                {
                    while (HasFileLoad(fullPath)) Monitor.Wait(Sync);
                    CacheEntry entry = IsRaw(path) && isEnabled ? FindEntry(fullPath) : null;
                    if (entry != null)
                    {
                        using (Stream current = Borrow(entry)) tail = update(current);
                        entry.Tail = tail;
                        Remember(entry, fullPath, checked(tail.Offset + tail.Bytes.Length));
                        if (entry.SaveMode == CVFileSaveMode.MemoryOnly) return;
                    }
                }
                using (FileStream file = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    CVFileMetadata.ReplaceTail(file, tail ?? update(file));
            }
        }

        private static bool IsRaw(string path) => string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase);
        private static FileStream OpenFile(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        private static CacheEntry FindEntry(string path)
            => Entries.Find(entry => !entry.IsRetired && !entry.IsLoading && string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase));

        // Only a writer to this same file waits for its disk reader; Monitor.Wait releases Sync.
        private static bool HasFileLoad(string path)
            => Entries.Any(entry => entry.IsLoading && string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase));

        private static CacheEntry FindOldest(bool requireIdle)
        {
            CacheEntry oldest = null;
            foreach (CacheEntry entry in Entries)
            {
                if (entry.IsRetired || (requireIdle && (entry.Readers != 0 || entry.Writers != 0))) continue;
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

        private static bool HasReaders()
        {
            foreach (CacheEntry entry in Entries)
            {
                if (entry.Readers != 0) return true;
            }
            return false;
        }

        private static void Remember(CacheEntry entry, string path, long fileLength)
        {
            entry.FilePath = path;
            entry.Length = fileLength;
            entry.LastAccessSequence = ++accessSequence;
        }

        private static void ForgetFile(CacheEntry entry)
        {
            entry.FilePath = null;
            entry.Length = 0;
            entry.Tail = null;
            entry.SaveMode = CVFileSaveMode.Synchronous;
            entry.HitCount = 0;
            entry.LastAccessSequence = ++accessSequence;
        }

        private static void Retire(CacheEntry entry)
        {
            entry.IsRetired = true;
            if (entry.Readers == 0 && entry.Writers == 0)
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

        private static Stream Borrow(CacheEntry entry)
        {
            Stream memory = OpenBuffer(entry, entry.Tail == null ? entry.Length : entry.Tail.Offset, FileAccess.Read);
            Stream result = new CachedReadStream(entry, memory, entry.Tail?.Bytes);
            entry.Readers++;
            entry.LastAccessSequence = ++accessSequence;
            return result;
        }

        private sealed class CacheEntry
        {
            internal IntPtr Buffer;
            internal long Capacity;
            internal long Length;
            internal string FilePath;
            internal int Readers;
            internal int Writers;
            internal FileTail Tail;
            internal CVFileSaveMode SaveMode;
            internal long HitCount;
            internal long AllocationCount;
            internal long LastAccessSequence;
            internal bool IsRetired;
            internal bool IsLoading;
        }

        internal sealed class FileTail
        {
            internal FileTail(long offset, byte[] bytes) { Offset = offset; Bytes = bytes; }
            internal long Offset { get; }
            internal byte[] Bytes { get; }
        }

        // Do not expose UnmanagedMemoryStream.PositionPointer or writable cache memory to consumers.
        private sealed class CachedReadStream : Stream
        {
            private CacheEntry entry;
            private readonly Stream memory;
            private readonly byte[] tail;
            private long position;
            internal CachedReadStream(CacheEntry entry, Stream memory, byte[] tail) { this.entry = entry; this.memory = memory; this.tail = tail; }
            ~CachedReadStream() { Dispose(false); }
            public override bool CanRead => entry != null;
            public override bool CanSeek => entry != null;
            public override bool CanWrite => false;
            public override long Length => memory.Length + (tail == null ? 0 : tail.Length);
            public override long Position { get => position; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); position = value; } }
            public override int Read(byte[] destination, int offset, int count)
            {
                if (destination == null) throw new ArgumentNullException(nameof(destination));
                if (offset < 0 || count < 0 || offset > destination.Length - count) throw new ArgumentOutOfRangeException(nameof(count));
#if NETCOREAPP
                return Read(destination.AsSpan(offset, count));
#else
                int read = 0;
                if (position < memory.Length)
                {
                    memory.Position = position;
                    read = memory.Read(destination, offset, (int)Math.Min(count, memory.Length - position));
                    position += read;
                }
                if (read < count && position < Length && tail != null)
                {
                    int remaining = (int)Math.Min(count - read, Length - position);
                    Buffer.BlockCopy(tail, checked((int)(position - memory.Length)), destination, offset + read, remaining);
                    position += remaining;
                    read += remaining;
                }
                return read;
#endif
            }
#if NETCOREAPP
            public override int Read(Span<byte> destination)
            {
                int read = 0;
                if (position < memory.Length)
                {
                    memory.Position = position;
                    read = memory.Read(destination.Slice(0, (int)Math.Min(destination.Length, memory.Length - position)));
                    position += read;
                }
                if (read < destination.Length && position < Length && tail != null)
                {
                    int remaining = (int)Math.Min(destination.Length - read, Length - position);
                    tail.AsSpan(checked((int)(position - memory.Length)), remaining).CopyTo(destination.Slice(read));
                    position += remaining;
                    read += remaining;
                }
                return read;
            }
#endif
            public override int ReadByte()
            {
                if (position >= Length) return -1;
                if (position >= memory.Length) return tail[checked((int)(position++ - memory.Length))];
                memory.Position = position++;
                return memory.ReadByte();
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = checked((origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? position : origin == SeekOrigin.End ? Length : throw new ArgumentOutOfRangeException(nameof(origin))) + offset);
                return position;
            }
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] source, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                CacheEntry owned = Interlocked.Exchange(ref entry, null);
                if (owned != null)
                {
                    try { memory.Dispose(); }
                    finally
                    {
                        lock (Sync)
                        {
                            owned.Readers--;
                            if (owned.IsRetired && owned.Readers == 0 && owned.Writers == 0)
                            {
                                FreeBuffer(owned);
                                Entries.Remove(owned);
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
