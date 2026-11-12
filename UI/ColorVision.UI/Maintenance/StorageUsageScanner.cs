using System.IO;
using System.IO.Enumeration;

namespace ColorVision.UI.Maintenance;

/// <summary>Accounts for allowlisted files without deleting, creating directories, or following links.</summary>
public static class StorageUsageScanner
{
    public static StorageUsageSnapshot Scan(IEnumerable<StorageUsageRule> rules, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var files = new List<FileMaintenanceEntry>();
        var issues = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool cancelled = false;
        long bytes = 0;
        try
        {
            foreach (StorageUsageRule rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string root;
                try
                {
                    if (string.IsNullOrWhiteSpace(rule.SearchPattern) || rule.SearchPattern.IndexOfAny(['\\', '/', ':']) >= 0)
                        throw new ArgumentException("存储文件模式只能匹配文件名。");
                    root = ValidateRoot(rule.RootPath);
                    FileAttributes attributes = File.GetAttributes(root);
                    if ((attributes & FileAttributes.Directory) == 0) throw new ArgumentException("存储来源必须是目录。");
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("存储统计不访问链接或联接点。");
                }
                catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException) { continue; }
                catch (Exception ex) when (IsRecoverable(ex)) { issues.Add($"{rule.RootPath} — {ex.Message}"); continue; }
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string directory = pending.Pop();
                    try
                    {
                        EnsureNoLinks(directory);
                        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                FileAttributes attributes = File.GetAttributes(path);
                                if ((attributes & FileAttributes.ReparsePoint) != 0)
                                {
                                    issues.Add($"{path} — 已跳过链接或重解析点。");
                                    continue;
                                }
                                if ((attributes & FileAttributes.Directory) != 0)
                                {
                                    if (rule.Recursive) pending.Push(path);
                                    continue;
                                }
                                if (!FileSystemName.MatchesSimpleExpression(rule.SearchPattern, Path.GetFileName(path), true)
                                    || (rule.IncludeFile != null && !rule.IncludeFile(path))) continue;
                                string fullPath = Path.GetFullPath(path);
                                if (!seen.Add(fullPath)) continue;
                                EnsureNoLinks(Path.GetDirectoryName(fullPath)!);
                                var file = new FileInfo(fullPath);
                                long length = file.Length;
                                bytes = checked(bytes + length);
                                files.Add(new(fullPath, length, file.LastWriteTimeUtc));
                            }
                            catch (Exception ex) when (IsRecoverable(ex)) { issues.Add($"{path} — {ex.Message}"); }
                        }
                    }
                    catch (Exception ex) when (IsRecoverable(ex)) { issues.Add($"{directory} — {ex.Message}"); }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { cancelled = true; }
        return new(bytes, files.Count, files.AsReadOnly(), issues.AsReadOnly(), cancelled);
    }

    private static string ValidateRoot(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("存储目录必须使用绝对路径。");
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (string.Equals(root, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("存储统计不能扫描磁盘或共享根目录。");
        EnsureNoLinks(root);
        return root;
    }

    private static void EnsureNoLinks(string path)
    {
        for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("存储统计不访问链接或联接点。");
    }

    private static bool IsRecoverable(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException;
}
