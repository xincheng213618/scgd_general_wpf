using System.IO;

namespace WindowsServicePlugin.ServiceManager;

public sealed record ServiceLogCleanupResult(int DeletedFileCount, long DeletedBytes, IReadOnlyList<string> Failures)
{
    public bool Succeeded => Failures.Count == 0;
}

public static class ServiceLogCleanup
{
    public static string? ResolveLogDirectory(ServiceEntry entry, string baseLocation)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            string? serviceDirectory;
            if (!string.IsNullOrWhiteSpace(entry.ExePath))
            {
                string executablePath = Path.GetFullPath(entry.ExePath);
                serviceDirectory = Path.GetDirectoryName(executablePath);
            }
            else if (!string.IsNullOrWhiteSpace(baseLocation) && !string.IsNullOrWhiteSpace(entry.FolderName))
            {
                string rootDirectory = Path.GetFullPath(baseLocation);
                serviceDirectory = Path.GetFullPath(Path.Combine(rootDirectory, entry.FolderName));
                if (!IsPathWithin(serviceDirectory, rootDirectory))
                    return null;
            }
            else
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(serviceDirectory) ? null : Path.Combine(serviceDirectory, "log");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static ServiceLogCleanupResult Clear(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        string fullLogDirectory = Path.GetFullPath(logDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(Path.GetFileName(fullLogDirectory), "log", StringComparison.OrdinalIgnoreCase))
        {
            return new ServiceLogCleanupResult(0, 0, [$"拒绝清理非日志目录: {fullLogDirectory}"]);
        }

        if (!Directory.Exists(fullLogDirectory))
            return new ServiceLogCleanupResult(0, 0, []);

        List<string> failures = [];
        string[] files;
        try
        {
            files = Directory.GetFiles(fullLogDirectory, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint,
            });
        }
        catch (Exception ex)
        {
            return new ServiceLogCleanupResult(0, 0, [$"读取日志目录失败: {ex.Message}"]);
        }

        int deletedFileCount = 0;
        long deletedBytes = 0;
        foreach (string file in files)
        {
            try
            {
                long length = new FileInfo(file).Length;
                FileAttributes attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);

                File.Delete(file);
                deletedFileCount++;
                deletedBytes += length;
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return new ServiceLogCleanupResult(deletedFileCount, deletedBytes, failures);
    }

    private static bool IsPathWithin(string candidatePath, string rootPath)
    {
        string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
