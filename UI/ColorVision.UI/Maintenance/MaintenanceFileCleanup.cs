using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Maintenance;

/// <summary>A caller-owned allowlist. Protection is checked again when the confirmed scan is executed.</summary>
public sealed record MaintenanceFileCleanupRule(
    string Id,
    string RootPath,
    string SearchPattern = "*",
    bool Recursive = false,
    int RetentionDays = 30,
    Func<string, bool>? IsProtected = null);

public enum MaintenanceFileCleanupIssueKind { Skipped, Failed }

public sealed record MaintenanceFileCleanupIssue(
    string RuleId,
    string FullPath,
    string Message,
    MaintenanceFileCleanupIssueKind Kind = MaintenanceFileCleanupIssueKind.Skipped);

public sealed class MaintenanceFileCleanupFile
{
    internal MaintenanceFileCleanupFile(MaintenanceFileCleanupRule rule, string path, MaintenanceFileCleanup.FileIdentity identity)
    {
        Rule = rule;
        FullPath = path;
        Identity = identity;
    }

    internal MaintenanceFileCleanupRule Rule { get; }
    internal MaintenanceFileCleanup.FileIdentity Identity { get; }
    public string RuleId => Rule.Id;
    public string FullPath { get; }
    public long Length => Identity.Length;
    public DateTime LastWriteTimeUtc => Identity.LastWriteTimeUtc;
}

/// <summary>An immutable, concrete deletion proposal. It never expands when executed.</summary>
public sealed class MaintenanceFileCleanupScanResult
{
    internal MaintenanceFileCleanupScanResult(List<MaintenanceFileCleanupFile> files, List<MaintenanceFileCleanupIssue> issues, bool isCancelled)
    {
        Files = Array.AsReadOnly(files.ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
        TotalBytes = files.Sum(file => file.Length);
        IsCancelled = isCancelled;
    }

    public IReadOnlyList<MaintenanceFileCleanupFile> Files { get; }
    public IReadOnlyList<MaintenanceFileCleanupIssue> Issues { get; }
    public long TotalBytes { get; }
    public bool IsCancelled { get; }
}

public sealed class MaintenanceFileCleanupResult
{
    internal MaintenanceFileCleanupResult(int deleted, int skipped, int failed, long bytes, bool cancelled, List<MaintenanceFileCleanupIssue> issues)
    {
        DeletedFileCount = deleted;
        SkippedFileCount = skipped;
        FailedFileCount = failed;
        DeletedBytes = bytes;
        IsCancelled = cancelled;
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public int DeletedFileCount { get; }
    public int SkippedFileCount { get; }
    public int FailedFileCount { get; }
    public long DeletedBytes { get; }
    public bool IsCancelled { get; }
    public IReadOnlyList<MaintenanceFileCleanupIssue> Issues { get; }
}

/// <summary>
/// Windows file-only cleanup. Paths are pinned against rename/reparse races; files are deleted
/// by the same exclusive handle whose identity was checked, never by reopening a path.
/// No directory, including an empty allowlist root, is deleted.
/// </summary>
public static class MaintenanceFileCleanup
{
    private const uint ReadAttributes = 0x80;
    private const uint DeleteAccess = 0x10000;
    private const uint ShareReadWrite = 3;
    private const uint ShareReadWriteDelete = 7;
    private const uint OpenExisting = 3;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint BackupSemantics = 0x02000000;

    public static MaintenanceFileCleanupScanResult Scan(IEnumerable<MaintenanceFileCleanupRule> rules, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var files = new List<MaintenanceFileCleanupFile>();
        var issues = new List<MaintenanceFileCleanupIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool cancelled = false;
        try
        {
            foreach (MaintenanceFileCleanupRule suppliedRule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MaintenanceFileCleanupRule rule;
                try
                {
                    rule = NormalizeRule(suppliedRule);
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    issues.Add(new(suppliedRule?.Id ?? string.Empty, suppliedRule?.RootPath ?? string.Empty, exception.Message, MaintenanceFileCleanupIssueKind.Failed));
                    continue;
                }

                DateTime cutoff = DateTime.UtcNow.AddDays(-rule.RetentionDays);
                var pending = new Stack<string>();
                pending.Push(rule.RootPath);
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string directory = pending.Pop();
                    try
                    {
                        using var directories = PinDirectories(directory);
                        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                FileAttributes attributes = File.GetAttributes(entry);
                                if ((attributes & FileAttributes.ReparsePoint) != 0)
                                    throw new CleanupSkipException("已跳过链接或重解析点。");
                                if ((attributes & FileAttributes.Directory) != 0)
                                {
                                    if (rule.Recursive)
                                        pending.Push(entry);
                                    continue;
                                }
                                if (!MatchesRule(rule, entry) || seen.Contains(entry))
                                    continue;

                                using SafeFileHandle handle = OpenHandle(entry, ReadAttributes, ShareReadWriteDelete, OpenReparsePoint);
                                FileIdentity identity = ReadIdentity(handle, isDirectory: false);
                                if (identity.LastWriteTimeUtc >= cutoff || rule.IsProtected?.Invoke(entry) == true)
                                    continue;
                                cancellationToken.ThrowIfCancellationRequested();
                                seen.Add(entry);
                                files.Add(new(rule, entry, identity));
                            }
                            catch (Exception exception) when (IsRecoverable(exception))
                            {
                                issues.Add(CreateIssue(rule.Id, entry, exception));
                            }
                        }
                    }
                    catch (Exception exception) when (IsRecoverable(exception))
                    {
                        // A missing optional cache directory is normal, not a failed scan.
                        if (!IsMissing(exception))
                            issues.Add(CreateIssue(rule.Id, directory, exception));
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
        }
        return new(files, issues, cancelled);
    }

    public static MaintenanceFileCleanupResult Cleanup(MaintenanceFileCleanupScanResult scan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var issues = new List<MaintenanceFileCleanupIssue>();
        int deleted = 0, skipped = 0, failed = 0;
        long bytes = 0;
        bool cancelled = false;
        try
        {
            foreach (MaintenanceFileCleanupFile file in scan.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    MaintenanceFileCleanupRule rule = file.Rule;
                    if (!MatchesRule(rule, file.FullPath))
                        throw new CleanupSkipException("文件已不属于确认的清理范围。");

                    using var directories = PinDirectories(Path.GetDirectoryName(file.FullPath)!);
                    // No sharing: skip active readers/writers. DELETE access lets us mark this
                    // exact file for deletion while retaining the exclusive validation handle.
                    using SafeFileHandle handle = OpenHandle(file.FullPath, ReadAttributes | DeleteAccess, 0, OpenReparsePoint);
                    FileIdentity current = ReadIdentity(handle, isDirectory: false);
                    if (current != file.Identity)
                        throw new CleanupSkipException("文件在扫描后发生变化，请重新扫描。");
                    if (current.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-rule.RetentionDays))
                        throw new CleanupSkipException("文件仍处于保留期。");
                    if (rule.IsProtected?.Invoke(file.FullPath) == true)
                        throw new CleanupSkipException("文件正在使用或受保护。");
                    cancellationToken.ThrowIfCancellationRequested();

                    var disposition = new FileDispositionInfo { DeleteFile = true };
                    if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<FileDispositionInfo>()))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    deleted++;
                    bytes += current.Length;
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    MaintenanceFileCleanupIssue issue = CreateIssue(file.RuleId, file.FullPath, exception);
                    issues.Add(issue);
                    if (issue.Kind == MaintenanceFileCleanupIssueKind.Skipped)
                        skipped++;
                    else
                        failed++;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
        }
        return new(deleted, skipped, failed, bytes, cancelled, issues);
    }

    private static MaintenanceFileCleanupRule NormalizeRule(MaintenanceFileCleanupRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule.Id);
        if (string.IsNullOrWhiteSpace(rule.RootPath) || !Path.IsPathFullyQualified(rule.RootPath))
            throw new ArgumentException("清理根目录必须是绝对路径。");
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rule.RootPath));
        if (string.Equals(root, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("不能将驱动器或网络共享根目录作为清理范围。");
        if (string.IsNullOrWhiteSpace(rule.SearchPattern) || rule.SearchPattern is "." or ".." || rule.SearchPattern.IndexOfAny(['/', '\\', ':']) >= 0)
            throw new ArgumentException("清理模式只能匹配文件名，不能包含路径。");
        if (rule.RetentionDays < 0)
            throw new ArgumentOutOfRangeException(nameof(rule), "保留天数不能小于零。");
        _ = DateTime.UtcNow.AddDays(-rule.RetentionDays);
        return rule with { RootPath = root };
    }

    private static bool MatchesRule(MaintenanceFileCleanupRule rule, string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!string.Equals(path, fullPath, StringComparison.OrdinalIgnoreCase)
            || !fullPath.StartsWith(rule.RootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!rule.Recursive && !string.Equals(Path.GetDirectoryName(fullPath), rule.RootPath, StringComparison.OrdinalIgnoreCase))
            return false;
        return FileSystemName.MatchesSimpleExpression(rule.SearchPattern, Path.GetFileName(fullPath), ignoreCase: true);
    }

    private static PinnedDirectories PinDirectories(string directory)
    {
        var paths = new Stack<string>();
        for (DirectoryInfo? item = new(directory); item != null; item = item.Parent)
            paths.Push(item.FullName);
        var result = new PinnedDirectories();
        try
        {
            while (paths.Count > 0)
            {
                SafeFileHandle handle = OpenHandle(paths.Pop(), ReadAttributes, ShareReadWrite, BackupSemantics | OpenReparsePoint);
                result.Handles.Add(handle);
                _ = ReadIdentity(handle, isDirectory: true);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static SafeFileHandle OpenHandle(string path, uint access, uint share, uint flags)
    {
        SafeFileHandle handle = CreateFile(path, access, share, IntPtr.Zero, OpenExisting, flags, IntPtr.Zero);
        if (!handle.IsInvalid)
            return handle;
        int error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error);
    }

    private static FileIdentity ReadIdentity(SafeFileHandle handle, bool isDirectory)
    {
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var attributes = (FileAttributes)info.FileAttributes;
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new CleanupSkipException("已跳过链接或重解析点。");
        if (((attributes & FileAttributes.Directory) != 0) != isDirectory)
            throw new CleanupSkipException("目标文件类型发生变化。");
        return new(info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow,
            ToFileTime(info.CreationTime), DateTime.FromFileTimeUtc(ToFileTime(info.LastWriteTime)),
            ((long)info.FileSizeHigh << 32) | info.FileSizeLow);
    }

    private static long ToFileTime(System.Runtime.InteropServices.ComTypes.FILETIME time) => ((long)(uint)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    private static bool IsRecoverable(Exception exception) => exception is not OperationCanceledException and not OutOfMemoryException;
    private static bool IsMissing(Exception exception) => exception is FileNotFoundException or DirectoryNotFoundException || exception is Win32Exception { NativeErrorCode: 2 or 3 };
    private static MaintenanceFileCleanupIssue CreateIssue(string ruleId, string path, Exception exception)
    {
        bool skipped = exception is CleanupSkipException || IsMissing(exception) || exception is Win32Exception { NativeErrorCode: 32 or 33 or 303 };
        string message = exception is Win32Exception { NativeErrorCode: 32 or 33 } ? "文件正在使用，已跳过。" : exception.Message;
        return new(ruleId, path, message, skipped ? MaintenanceFileCleanupIssueKind.Skipped : MaintenanceFileCleanupIssueKind.Failed);
    }

    internal readonly record struct FileIdentity(uint Volume, ulong FileIndex, long CreationTime, DateTime LastWriteTimeUtc, long Length);
    private sealed class CleanupSkipException(string message) : IOException(message);
    private sealed class PinnedDirectories : IDisposable
    {
        public List<SafeFileHandle> Handles { get; } = new();
        public void Dispose()
        {
            for (int index = Handles.Count - 1; index >= 0; index--)
                Handles[index].Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.U1)] public bool DeleteFile;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int fileInformationClass, ref FileDispositionInfo information, uint bufferSize);
}
