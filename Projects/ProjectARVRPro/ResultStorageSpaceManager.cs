using log4net;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ProjectARVRPro;

/// <summary>Checks output volumes before writing; only recognized ARVR exports are cleanup candidates.</summary>
internal sealed class ResultStorageSpaceManager
{
    internal const long BytesPerGB = 1024L * 1024 * 1024;
    private static readonly ILog Log = LogManager.GetLogger(typeof(ResultStorageSpaceManager));
    internal static ResultStorageSpaceManager Instance { get; } = new();
    private static readonly Regex CsvName = new(@"^TestResults_.*_\d{8}_\d{6}_\.csv$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex XlsxName = new(@"^(\d{4}-\d{1,2}-\d{1,2})TestResults\+.+\.xlsx$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly TimeSpan CandidateLifetime = TimeSpan.FromSeconds(30);
    private const int MaximumCachedRoots = 4;
    private readonly SemaphoreSlim cleanupGate = new(1, 1);
    private readonly object writeGate = new();
    private readonly Dictionary<Guid, string[]> activeWrites = [];
    private readonly Dictionary<string, CandidateSnapshot> candidateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, (long Available, long Total)>? readSpace;
    private readonly Action<string> warn;
    private readonly TimeProvider clock;

    internal ResultStorageSpaceManager(Func<string, (long Available, long Total)>? readSpace = null, Action<string>? warn = null, TimeProvider? clock = null)
    {
        this.readSpace = readSpace;
        this.warn = warn ?? (message => Log.Warn(message));
        this.clock = clock ?? TimeProvider.System;
    }

    // Register protection before scheduling background cleanup, and retain it through staging-file promotion.
    internal WriteLease BeginWrite(string rootDirectory, bool enabled, int minimumFreeSpaceGB, params string[] protectedPaths)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        string[] paths = protectedPaths.Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))).ToArray();
        Guid id = Guid.NewGuid();
        lock (writeGate) activeWrites.Add(id, paths);
        return new(this, id, root, enabled, minimumFreeSpaceGB);
    }

    internal sealed class WriteLease(ResultStorageSpaceManager owner, Guid id, string root, bool enabled, int minimumFreeSpaceGB) : IDisposable
    {
        internal Task EnsureSpaceAsync() => enabled ? owner.EnsureSpaceAsync(root, minimumFreeSpaceGB) : Task.CompletedTask;

        public void Dispose()
        {
            lock (owner.writeGate) owner.activeWrites.Remove(id);
        }
    }

    private async Task EnsureSpaceAsync(string root, int minimumFreeSpaceGB)
    {
        // Waiters do not occupy worker threads, and all disk I/O stays off the caller's dispatcher.
        await cleanupGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => EnsureSpace(root, minimumFreeSpaceGB)).ConfigureAwait(false);
        }
        finally
        {
            cleanupGate.Release();
        }
    }

    private void EnsureSpace(string root, int minimumFreeSpaceGB)
    {
        try
        {
            if (minimumFreeSpaceGB <= 0) throw new InvalidOperationException("保留磁盘空间必须大于0 GB。");
            if (string.Equals(root, Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("结果输出目录不能直接使用磁盘根目录进行自动清理。");
            if (HasReparsePoint(root)) throw new IOException("输出路径包含符号链接、目录联接或挂载点，已跳过自动清理。");

            long minimumBytes = minimumFreeSpaceGB * BytesPerGB;
            var space = readSpace?.Invoke(root) ?? ReadLocalDiskSpace(root);
            if (space.Available < 0) throw new IOException("无法获取输出盘的剩余空间，已跳过自动清理。");
            if (space.Total <= minimumBytes) throw new InvalidOperationException("保留空间必须小于输出盘的总容量，已跳过自动清理。");
            if (space.Available >= minimumBytes) return; // Healthy volumes never scan the output tree.

            var scanTiming = Stopwatch.StartNew();
            CandidateSnapshot snapshot = GetCandidates(root, out bool cacheHit);
            scanTiming.Stop();
            int candidateCount = snapshot.Files.Count;
            var deleteTiming = Stopwatch.StartNew();
            int deleted = 0;
            long deletedBytes = 0;
            for (LinkedListNode<FileInfo>? node = snapshot.Files.First; node != null;)
            {
                LinkedListNode<FileInfo> currentNode = node;
                node = node.Next;
                FileInfo file = currentNode.Value;
                bool removed = false;
                lock (writeGate)
                {
                    if (IsProtected(file.FullName)) continue;
                    try
                    {
                        // Recheck the absolute boundary and links immediately before each file deletion.
                        if (!IsWithin(file.FullName, root) || HasReparsePoint(file.FullName))
                        {
                            snapshot.Files.Remove(currentNode);
                            continue;
                        }
                        var current = new FileInfo(file.FullName);
                        if (!current.Exists || current.LastWriteTimeUtc != file.LastWriteTimeUtc || current.Length != file.Length)
                        {
                            // A changed/replaced export must wait for a fresh scan and chronological sort.
                            snapshot.Files.Remove(currentNode);
                            continue;
                        }
                        File.Delete(file.FullName);
                        snapshot.Files.Remove(currentNode);
                        removed = true;
                        deleted++;
                        deletedBytes += file.Length;
                        if (Log.IsDebugEnabled) Log.Debug($"ARVR空间清理已删除导出文件：{file.FullName}");
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        warn($"ARVR空间清理跳过无法删除的文件：{file.FullName}；{ex.Message}");
                    }
                }
                if (removed)
                {
                    // Keep the exact stop condition: allocated size, compression and other writers
                    // mean logical file lengths cannot replace a real free-space query.
                    space.Available = readSpace?.Invoke(root).Available ?? new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace;
                    if (space.Available < 0) throw new IOException("无法获取输出盘的剩余空间，已停止自动清理。");
                    if (space.Available >= minimumBytes) break;
                }
            }
            Log.Info($"ARVR空间清理完成：Directory={root} CacheHit={cacheHit} Candidates={candidateCount} DeletedFiles={deleted} DeletedMiB={deletedBytes / (1024d * 1024):F2} ScanMs={scanTiming.Elapsed.TotalMilliseconds:F3} DeleteMs={deleteTiming.Elapsed.TotalMilliseconds:F3}，剩余{space.Available / (double)BytesPerGB:F2} GB。");
            if (space.Available < minimumBytes)
                warn($"ARVR输出盘剩余{space.Available / (double)BytesPerGB:F2} GB，未达到保留{minimumFreeSpaceGB} GB；可清理的历史导出不足，继续尝试保存。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            candidateCache.Remove(root);
            // Cleanup failure must not change measurement, database persistence, or the existing export error handling.
            warn($"ARVR磁盘空间检查失败，继续尝试保存：{ex.Message}");
        }
    }

    private CandidateSnapshot GetCandidates(string root, out bool cacheHit)
    {
        long now = clock.GetTimestamp();
        foreach (string expiredRoot in candidateCache.Where(pair => clock.GetElapsedTime(pair.Value.CreatedAt, now) >= CandidateLifetime).Select(pair => pair.Key).ToArray())
            candidateCache.Remove(expiredRoot);
        cacheHit = candidateCache.TryGetValue(root, out CandidateSnapshot? cached);
        if (cached != null) return cached;

        List<FileInfo> files = [];
        if (Directory.Exists(root)) CollectCandidates(root, files);
        files.Sort((left, right) =>
        {
            int order = left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc);
            return order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(left.FullName, right.FullName);
        });
        var snapshot = new CandidateSnapshot(clock.GetTimestamp(), new LinkedList<FileInfo>(files));
        if (candidateCache.Count >= MaximumCachedRoots)
            candidateCache.Remove(candidateCache.MinBy(pair => pair.Value.CreatedAt).Key);
        candidateCache[root] = snapshot;
        return snapshot;
    }

    private sealed record CandidateSnapshot(long CreatedAt, LinkedList<FileInfo> Files);

    private void CollectCandidates(string directory, List<FileInfo> candidates)
    {
        // Include active outputs in the snapshot; the lease is checked again under writeGate at deletion.
        // This lets a subsequent save reuse the scan after an earlier writer releases its protection.
        try
        {
            foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (entry is DirectoryInfo child) CollectCandidates(child.FullName, candidates);
                else if (entry is FileInfo file && IsExportFile(file.Name)) candidates.Add(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warn($"ARVR空间清理跳过无法读取的目录：{directory}；{ex.Message}");
        }
    }

    private static bool IsExportFile(string name)
    {
        if (CsvName.IsMatch(name)) return true;
        Match xlsx = XlsxName.Match(name);
        if (xlsx.Success)
        {
            // Daily reports append throughout the day; do not erase today's aggregate between writes.
            return DateTime.TryParseExact(xlsx.Groups[1].Value, "yyyy-M-d", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                && date.Date < DateTime.Today;
        }
        string extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp")) return false;
        string stem = Path.GetFileNameWithoutExtension(name);
        return stem.Contains('_') && (stem.EndsWith("result", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("source", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsProtected(string path) => activeWrites.Values.SelectMany(paths => paths).Any(protectedPath =>
        string.Equals(path, protectedPath, StringComparison.OrdinalIgnoreCase) || IsWithin(path, protectedPath));

    private static bool IsWithin(string path, string directory) => path.StartsWith(
        Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool HasReparsePoint(string path)
    {
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return false;
    }

    private static (long Available, long Total) ReadLocalDiskSpace(string root)
    {
        string volume = Path.GetPathRoot(root) ?? throw new IOException("输出目录没有有效的磁盘。");
        if (volume.StartsWith(@"\\", StringComparison.Ordinal)) throw new IOException("网络共享不支持本地磁盘自动清理。");
        var drive = new DriveInfo(volume);
        return (drive.AvailableFreeSpace, drive.TotalSize);
    }
}
