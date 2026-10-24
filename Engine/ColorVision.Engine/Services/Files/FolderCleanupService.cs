using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Files;

internal sealed class FolderCleanupResult
{
    public int FolderCount { get; set; }
    public int ScannedFiles { get; set; }
    public int DeletedFiles { get; set; }
    public int FailedFiles { get; set; }
    public int ProtectedFiles { get; set; }
    public int ChangedFiles { get; set; }
    public long DeletedBytes { get; set; }
    public long RemainingBytes { get; set; }
}

internal static class FolderCleanupService
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(FolderCleanupService));
    private static readonly char[] ExtensionSeparators = [',', ';', ' ', '\t', '\r', '\n'];
    private sealed record Candidate(string Path, long Length, DateTime LastWriteTimeUtc);

    public static FolderCleanupResult Execute(IEnumerable<string> folders, string fileExtensions, bool includeSubfolders,
        long triggerBytes, long targetBytes, IReadOnlyCollection<string>? protectedFiles = null, Action? checkCancellation = null)
    {
        // Validate every configured root before the first deletion.
        string[] roots = folders.Where(path => !string.IsNullOrWhiteSpace(path)).Select(ValidateRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        HashSet<string> extensions = ParseExtensions(fileExtensions);
        HashSet<string> protectedPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in protectedFiles ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(path)) protectedPaths.Add(Path.GetFullPath(path));

        var result = new FolderCleanupResult();
        foreach (string root in roots)
        {
            checkCancellation?.Invoke();
            if (!Directory.Exists(root))
            {
                Log.Warn($"Cache cleanup directory does not exist: {root}");
                continue;
            }
            result.FolderCount++;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = includeSubfolders,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false
            };
            long totalBytes = 0;
            var candidates = new List<Candidate>();
            foreach (FileInfo file in new DirectoryInfo(root).EnumerateFiles("*", options))
            {
                checkCancellation?.Invoke();
                result.ScannedFiles++;
                if (extensions.Count > 0 && !extensions.Contains(file.Extension)) continue;
                try
                {
                    long length = file.Length;
                    totalBytes = checked(totalBytes + length);
                    candidates.Add(new Candidate(file.FullName, length, file.LastWriteTimeUtc));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    result.FailedFiles++;
                    Log.Warn($"Cannot read cleanup file: {file.FullName}", ex);
                }
            }

            long deletedBytes = 0;
            if (totalBytes > triggerBytes)
            {
                foreach (Candidate candidate in candidates.OrderBy(file => file.LastWriteTimeUtc).ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase))
                {
                    checkCancellation?.Invoke();
                    if (totalBytes - deletedBytes <= targetBytes) break;
                    if (protectedPaths.Contains(candidate.Path))
                    {
                        result.ProtectedFiles++;
                        continue;
                    }
                    try
                    {
                        ValidateFile(root, candidate.Path);
                        var current = new FileInfo(candidate.Path);
                        // A file changed after scanning may still be written by another process.
                        if (!current.Exists || current.Length != candidate.Length || current.LastWriteTimeUtc != candidate.LastWriteTimeUtc)
                        {
                            result.ChangedFiles++;
                            continue;
                        }
                        checkCancellation?.Invoke();
                        File.Delete(candidate.Path);
                        deletedBytes += candidate.Length;
                        result.DeletedFiles++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        result.FailedFiles++;
                        Log.Warn($"Cannot delete cleanup file: {candidate.Path}", ex);
                    }
                }
            }
            result.DeletedBytes += deletedBytes;
            result.RemainingBytes += totalBytes - deletedBytes;
            Log.Info($"Cache cleanup: Directory={root}, MatchedFiles={candidates.Count}, BeforeBytes={totalBytes}, DeletedBytes={deletedBytes}, RemainingBytes={totalBytes - deletedBytes}, TriggerBytes={triggerBytes}, TargetBytes={targetBytes}");
        }
        return result;
    }

    private static string ValidateRoot(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException(EngineLocalization.Format($"清理目录必须使用绝对路径：{path}"));
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (string.Equals(fullPath, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath)!), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(EngineLocalization.Format($"清理目录不能是磁盘或共享根目录：{path}"));
        EnsureNoLinks(fullPath);
        return fullPath;
    }

    private static void ValidateFile(string root, string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException(EngineLocalization.Format($"清理文件超出指定目录：{path}"));
        EnsureNoLinks(Path.GetDirectoryName(fullPath)!);
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new IOException(EngineLocalization.Format($"清理时不访问链接或联接点：{path}"));
    }

    private static void EnsureNoLinks(string path)
    {
        for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
        {
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException(EngineLocalization.Format($"清理时不访问链接或联接点：{directory.FullName}"));
        }
    }

    private static HashSet<string> ParseExtensions(string value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in (value ?? string.Empty).Split(ExtensionSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            string extension = part.Trim();
            result.Add(extension.StartsWith('.') ? extension : "." + extension);
        }
        return result;
    }
}
