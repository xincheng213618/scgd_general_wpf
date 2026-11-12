using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
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
        private static long cacheGeneration;
        private static bool isEnabled;
        private static int maximumEntries = 1;
        private static readonly object WriteSync = new object();
        private static readonly HashSet<string> PendingDiskWrites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                        cacheGeneration++;
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
                cacheGeneration++;
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
        /// With no reusable disk-backed slot, a miss reads disk without evicting a memory-only capture.
        /// </summary>
        public static Stream OpenRead(string filePath, bool populateCache = true)
        {
            if (!IsRaw(filePath)) return OpenFile(filePath);
            string path = Path.GetFullPath(filePath);
            return OpenReadCore(path, populateCache, () => OpenFile(path), publication: null);
        }

        internal static Stream OpenImageRead(string filePath, Func<Stream, bool> canPopulate)
        {
            if (!IsRaw(filePath)) return OpenFile(filePath);
            string path = Path.GetFullPath(filePath);
            Stream source = OpenReadCore(path, false, () => OpenFile(path), publication: null);
            if (source is not FileStream disk || !IsEnabled) return source;

            try
            {
                bool valid = canPopulate(disk);
                disk.Position = 0;
                if (!valid) return disk;

                // Warm the cache from the handle already checked, without reopening the path.
                Stream snapshot = OpenReadCore(path, true, () => disk, publication: null, countLookup: false);
                if (!ReferenceEquals(snapshot, disk)) disk.Dispose();
                return snapshot;
            }
            catch
            {
                disk.Dispose();
                throw;
            }
        }

        private static unsafe Stream OpenReadCore(string path, bool populateCache, Func<Stream> openFile, CommittedWrite? publication,
            bool countLookup = true)
        {
            bool committedWrite = publication.HasValue;
            CacheEntry entry = null;
            lock (Sync)
            {
                if (publication is { } published && published.Generation != cacheGeneration) populateCache = false;
                CacheEntry existing = isEnabled ? FindEntry(path) : null;
                if (existing != null && !committedWrite)
                {
                    if (countLookup)
                    {
                        hits++;
                        existing.HitCount++;
                    }
                    return Borrow(existing);
                }
                if (isEnabled)
                {
                    if (!committedWrite && countLookup) misses++;
                    if (committedWrite && existing != null && (!populateCache || existing.Readers != 0))
                    {
                        Retire(existing);
                        existing = null;
                    }
                    // During staging, misses may read the old file without racing its replacement into the cache.
                    if (populateCache && (committedWrite || !PendingDiskWrites.Contains(path)))
                        entry = existing ?? GetWritableEntry();
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
                if (entry == null && committedWrite) return Stream.Null;
                file = openFile();
                if (entry == null) return file;
                long length = file.Length;
                if (length <= 0 || length > int.MaxValue) return file;
                lock (Sync) EnsureCapacity(entry, length);
                if (committedWrite) file.ReadExactly(new Span<byte>(entry.Buffer.ToPointer(), (int)length));
                else using (Stream destination = OpenBuffer(entry, length, FileAccess.Write))
                {
                    file.CopyTo(destination);
                    if (destination.Position != length) throw new EndOfStreamException("Incomplete CVRAW cache snapshot.");
                }
                DateTime? lastWriteTimeUtc = file is FileStream disk ? TryGetLastWriteTimeUtc(disk) : publication?.LastWriteTimeUtc;
                file.Dispose();
                file = null;
                lock (Sync)
                {
                    entry.Length = length;
                    entry.DiskLastWriteTimeUtc = lastWriteTimeUtc;
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
            string fullPath = Path.GetFullPath(path);
            if (!IsRaw(path))
            {
                if (saveMode != CVFileSaveMode.Synchronous) throw new NotSupportedException("Memory-only saving requires CVRAW.");
                WriteSynchronous(fullPath, fileLength, write, updateRawCache: false);
                return true;
            }
            lock (WriteSync)
            {
                if (saveMode == CVFileSaveMode.MemoryOnly) WriteMemoryOnly(fullPath, fileLength, write);
                else WriteSynchronous(fullPath, fileLength, write, updateRawCache: true);
            }
            return true;
        }

        private static void WriteMemoryOnly(string path, long fileLength, Action<Stream> write)
        {
            lock (Sync)
            {
                while (HasFileLoad(path)) Monitor.Wait(Sync);
                if (!isEnabled) throw new InvalidOperationException("Memory-only saving requires the CVRAW cache.");
                if (fileLength <= 0 || fileLength > int.MaxValue)
                    throw new InvalidOperationException("The CVRAW snapshot could not be cached.");

                // A published memory-only snapshot may be the only copy. Keep all existing
                // keys and buffers intact until the streaming writer has produced a complete image.
                long generation = cacheGeneration;
                CacheEntry staged = new CacheEntry();
                try
                {
                    EnsureCapacity(staged, fileLength);
                    using (Stream memory = OpenBuffer(staged, fileLength, FileAccess.Write))
                    {
                        write(memory);
                        if (memory.Position != fileLength) throw new InvalidDataException("CVRAW writer did not write its declared length.");
                    }
                    if (cacheGeneration != generation)
                        throw new InvalidOperationException("The CVRAW cache was cleared during the save.");

                    CacheEntry replaced = FindEntry(path);
                    if (replaced == null && Entries.Count(entry => !entry.IsRetired) >= maximumEntries)
                        replaced = FindOldest(requireIdle: true) ?? FindOldest(requireIdle: false);

                    // Reserve the list entry before retiring any published data.
                    Entries.Add(staged);
                    if (replaced != null) Retire(replaced);
                    staged.SaveMode = CVFileSaveMode.MemoryOnly;
                    Remember(staged, path, fileLength);
                }
                catch { Retire(staged); throw; }
            }
        }

        private static void WriteSynchronous(string path, long fileLength, Action<Stream> write, bool updateRawCache)
        {
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".__colorvision_" + Guid.NewGuid().ToString("N") + ".tmp");
            bool registered = false;
            bool populateCache = false;
            long generation = 0;
            if (updateRawCache)
            {
                lock (Sync)
                {
                    while (HasFileLoad(path)) Monitor.Wait(Sync);
                    registered = PendingDiskWrites.Add(path);
                    populateCache = isEnabled && fileLength > 0 && fileLength <= int.MaxValue;
                    generation = cacheGeneration;
                }
            }
            bool ownsTemporary = false;
            bool committed = false;
            string backup = null;
            try
            {
                bool replaceExisting = CanReplaceExistingFile(path);
                using (FileStream file = CreateStagingFile(temporary, replaceExisting ? path : null))
                {
                    ownsTemporary = true;
                    write(file);
                    if (file.Position != fileLength || file.Length != fileLength)
                        throw new InvalidDataException("ColorVision image writer did not write its declared length.");
                }

                // Keep an exact read-only view without an open file handle blocking File.Replace.
                DateTime? lastWriteTimeUtc = null;
                using (Stream saved = populateCache ? TryMapStagedFile(temporary, fileLength, out lastWriteTimeUtc) : null)
                {
                    if (replaceExisting)
                    {
                        if (!CanReplaceExistingFile(path)) throw new FileNotFoundException("The image replacement target was removed.", path);
                        backup = Path.ChangeExtension(temporary, ".bak");
                        ReplaceFile(temporary, path, backup);
                    }
                    else File.Move(temporary, path);
                    committed = true;
                    if (updateRawCache)
                    {
                        using (OpenReadCore(path, saved != null, () => saved, new CommittedWrite(generation, lastWriteTimeUtc))) { }
                    }
                }
            }
            catch (Exception ex) when (committed && (ex is IOException || ex is UnauthorizedAccessException || ex is OutOfMemoryException))
            {
                Debug.WriteLine("CVRAW cache unavailable: " + ex.Message);
            }
            finally
            {
                if (registered) lock (Sync) PendingDiskWrites.Remove(path);
                if (committed && backup != null) TryDeleteStagingFile(backup);
                else if (ownsTemporary && !committed) TryDeleteStagingFile(temporary);
            }
        }

        private static FileStream CreateStagingFile(string temporary, string existing)
        {
            if (existing != null && OperatingSystem.IsWindows())
            {
                FileSecurity security = new FileInfo(existing).GetAccessControl(AccessControlSections.Access);
                return new FileInfo(temporary).Create(FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security);
            }
            return new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }

        private static bool CanReplaceExistingFile(string path)
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { return false; }
            const FileAttributes protectedAttributes = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint;
            if ((attributes & protectedAttributes) != 0)
                throw new UnauthorizedAccessException("A protected file or link cannot be replaced by a synchronous image save.");
            // Replacing a directory entry can succeed even when the existing file is not writable.
            using (new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)) { }
            return true;
        }

        private static void ReplaceFile(string temporary, string path, string backup)
        {
            try { File.Replace(temporary, path, backup); }
            catch
            {
                // Windows can move the original to the backup name before failing the replacement.
                if (File.Exists(backup))
                {
                    try { File.Move(backup, path); }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        Trace.TraceWarning($"ColorVision image original retained at '{backup}': {ex.Message}");
                    }
                }
                throw;
            }
        }

        private static void TryDeleteStagingFile(string path)
        {
            try { File.Delete(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.WriteLine($"CVRAW staging cleanup failed for '{path}': {ex.Message}");
            }
        }

        private static Stream TryMapStagedFile(string path, long length, out DateTime? lastWriteTimeUtc)
        {
            lastWriteTimeUtc = null;
            try
            {
                using (FileStream file = OpenFile(path))
                using (MemoryMappedFile map = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true))
                {
                    lastWriteTimeUtc = TryGetLastWriteTimeUtc(file);
                    return map.CreateViewStream(0, length, MemoryMappedFileAccess.Read);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is OutOfMemoryException)
            {
                Debug.WriteLine("CVRAW cache unavailable: " + ex.Message);
                return null;
            }
        }

        private static DateTime? TryGetLastWriteTimeUtc(FileStream file)
        {
            try { return File.GetLastWriteTimeUtc(file.SafeFileHandle); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return null;
            }
        }

        // Publish a complete metadata tail; existing readers retain their previous tail.
        internal static void UpdateMetadata(string path, Func<Stream, FileTail> update)
        {
            string fullPath = Path.GetFullPath(path);
            lock (WriteSync)
            {
                CacheEntry entry;
                FileTail tail = null;
                Stream cached = null;
                lock (Sync)
                {
                    while (HasFileLoad(fullPath)) Monitor.Wait(Sync);
                    entry = IsRaw(path) && isEnabled ? FindEntry(fullPath) : null;
                    if (entry != null)
                    {
                        if (entry.SaveMode == CVFileSaveMode.MemoryOnly)
                        {
                            using (Stream current = Borrow(entry)) PublishMetadata(entry, fullPath, update(current));
                            return;
                        }
                        // A read miss may otherwise reuse this slot while the disk tail is being written.
                        cached = Borrow(entry);
                        entry.Writers++;
                    }
                }
                bool written = false;
                DateTime? updatedCacheTimestamp = null;
                try
                {
                    using (cached)
                    using (FileStream file = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // A cached layout may predate an external replacement of the disk file.
                        tail = update(file);
                        DateTime? diskTimestamp = cached == null ? null : TryGetLastWriteTimeUtc(file);
                        bool sameTimestamp = diskTimestamp.HasValue && diskTimestamp == entry.DiskLastWriteTimeUtc;
                        if (CVFileMetadata.ReplaceTail(file, tail, sameTimestamp ? cached : null))
                            updatedCacheTimestamp = TryGetLastWriteTimeUtc(file);
                    }
                    written = true;
                }
                finally
                {
                    if (entry != null)
                    {
                        lock (Sync)
                        {
                            entry.Writers--;
                            if (entry.IsRetired || (written && !updatedCacheTimestamp.HasValue)) Retire(entry);
                            else if (written)
                            {
                                entry.DiskLastWriteTimeUtc = updatedCacheTimestamp;
                                PublishMetadata(entry, fullPath, tail);
                            }
                            Monitor.PulseAll(Sync);
                        }
                    }
                }
            }
        }

        private static void PublishMetadata(CacheEntry entry, string path, FileTail tail)
        {
            long length = checked(tail.Offset + tail.Bytes.Length);
            entry.Tail = tail;
            Remember(entry, path, length);
        }

        private static bool IsRaw(string path) => string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase);
        private static FileStream OpenFile(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        private static CacheEntry FindEntry(string path)
        {
            foreach (CacheEntry entry in Entries)
            {
                if (!entry.IsRetired && !entry.IsLoading && string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }

        // Only a writer to this same file waits for its disk reader; Monitor.Wait releases Sync.
        private static bool HasFileLoad(string path)
            => Entries.Any(entry => entry.IsLoading && string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase));

        private static CacheEntry FindOldest(bool requireIdle, bool diskBackedOnly = false)
        {
            CacheEntry oldest = null;
            foreach (CacheEntry entry in Entries)
            {
                if (entry.IsRetired || (diskBackedOnly && entry.SaveMode == CVFileSaveMode.MemoryOnly)
                    || (requireIdle && (entry.Readers != 0 || entry.Writers != 0))) continue;
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
            // A disk cache fill must never discard the only copy of an unrelated capture.
            return FindOldest(requireIdle: true, diskBackedOnly: true);
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
            entry.DiskLastWriteTimeUtc = null;
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
            internal DateTime? DiskLastWriteTimeUtc;
            internal CVFileSaveMode SaveMode;
            internal long HitCount;
            internal long AllocationCount;
            internal long LastAccessSequence;
            internal bool IsRetired;
            internal bool IsLoading;
        }

        private readonly record struct CommittedWrite(long Generation, DateTime? LastWriteTimeUtc);

        internal sealed class FileTail
        {
            internal FileTail(long pixelStart, long offset, byte[] bytes) { PixelStart = pixelStart; Offset = offset; Bytes = bytes; }
            internal long PixelStart { get; }
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
