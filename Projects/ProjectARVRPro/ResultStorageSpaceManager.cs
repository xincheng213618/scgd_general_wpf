using log4net;
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
    private readonly object cleanupGate = new();
    private readonly object writeGate = new();
    private readonly Dictionary<Guid, string[]> activeWrites = [];
    private readonly Func<string, (long Available, long Total)> readSpace;
    private readonly Action<string> warn;

    internal ResultStorageSpaceManager(Func<string, (long Available, long Total)>? readSpace = null, Action<string>? warn = null)
    {
        this.readSpace = readSpace ?? ReadLocalDiskSpace;
        this.warn = warn ?? (message => Log.Warn(message));
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
        internal void EnsureSpace()
        {
            if (enabled) owner.EnsureSpace(root, minimumFreeSpaceGB);
        }

        public void Dispose()
        {
            lock (owner.writeGate) owner.activeWrites.Remove(id);
        }
    }

    private void EnsureSpace(string root, int minimumFreeSpaceGB)
    {
        lock (cleanupGate)
        {
            try
            {
                if (minimumFreeSpaceGB <= 0) throw new InvalidOperationException("保留磁盘空间必须大于0 GB。");
                if (string.Equals(root, Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("结果输出目录不能直接使用磁盘根目录进行自动清理。");
                if (HasReparsePoint(root)) throw new IOException("输出路径包含符号链接、目录联接或挂载点，已跳过自动清理。");

                long minimumBytes = minimumFreeSpaceGB * BytesPerGB;
                var space = readSpace(root);
                if (space.Available < 0) throw new IOException("无法获取输出盘的剩余空间，已跳过自动清理。");
                if (space.Total <= minimumBytes) throw new InvalidOperationException("保留空间必须小于输出盘的总容量，已跳过自动清理。");
                if (space.Available >= minimumBytes) return; // Healthy volumes never scan the output tree.

                List<FileInfo> candidates = [];
                if (Directory.Exists(root)) CollectCandidates(root, candidates);
                int deleted = 0;
                foreach (FileInfo file in candidates.OrderBy(file => file.LastWriteTimeUtc).ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    bool removed = false;
                    lock (writeGate)
                    {
                        if (IsProtected(file.FullName)) continue;
                        try
                        {
                            // Recheck the absolute boundary and links immediately before each file deletion.
                            if (!IsWithin(file.FullName, root) || HasReparsePoint(file.FullName) || !file.Exists) continue;
                            if (File.GetLastWriteTimeUtc(file.FullName) != file.LastWriteTimeUtc) continue;
                            File.Delete(file.FullName);
                            removed = true;
                            deleted++;
                            Log.Info($"ARVR空间清理已删除导出文件：{file.FullName}");
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            warn($"ARVR空间清理跳过无法删除的文件：{file.FullName}；{ex.Message}");
                        }
                    }
                    if (removed)
                    {
                        space = readSpace(root);
                        if (space.Available >= minimumBytes) break;
                    }
                }
                if (deleted > 0) Log.Info($"ARVR空间清理完成：删除{deleted}个导出文件，剩余{space.Available / (double)BytesPerGB:F2} GB。");
                if (space.Available < minimumBytes)
                    warn($"ARVR输出盘剩余{space.Available / (double)BytesPerGB:F2} GB，未达到保留{minimumFreeSpaceGB} GB；可清理的历史导出不足，继续尝试保存。");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
            {
                // Cleanup failure must not change measurement, database persistence, or the existing export error handling.
                warn($"ARVR磁盘空间检查失败，继续尝试保存：{ex.Message}");
            }
        }
    }

    private void CollectCandidates(string directory, List<FileInfo> candidates)
    {
        lock (writeGate) if (IsProtected(directory)) return;
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
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
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
