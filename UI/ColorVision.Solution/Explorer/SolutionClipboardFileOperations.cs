using System.IO;

namespace ColorVision.Solution.Explorer
{
    internal sealed record SolutionFileOperationFailure(string SourcePath, string Message);

    internal sealed class SolutionFileOperationResult
    {
        public int RequestedCount { get; }
        public int SucceededCount { get; }
        public IReadOnlyList<SolutionFileOperationFailure> Failures { get; }
        public bool IsComplete => RequestedCount > 0 && SucceededCount == RequestedCount;

        public SolutionFileOperationResult(
            int requestedCount,
            int succeededCount,
            IReadOnlyList<SolutionFileOperationFailure> failures)
        {
            RequestedCount = requestedCount;
            SucceededCount = succeededCount;
            Failures = failures;
        }
    }

    /// <summary>
    /// Deterministic physical copy/move executor used by both clipboard paste and
    /// drag-and-drop. It reports skipped and failed sources instead of silently
    /// losing the operation state.
    /// </summary>
    internal static class SolutionClipboardFileOperations
    {
        public static SolutionFileOperationResult Execute(
            IEnumerable<string> sourcePaths,
            string targetDirectory,
            bool isMove)
        {
            ArgumentNullException.ThrowIfNull(sourcePaths);
            List<string> sources = sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var failures = new List<SolutionFileOperationFailure>();
            int succeededCount = 0;

            if (!Directory.Exists(targetDirectory))
            {
                failures.AddRange(sources.Select(path => new SolutionFileOperationFailure(
                    path,
                    "目标文件夹不存在。")));
                return new SolutionFileOperationResult(sources.Count, 0, failures);
            }

            foreach (string sourcePath in sources)
            {
                try
                {
                    if (File.Exists(sourcePath))
                    {
                        CopyOrMoveFile(sourcePath, targetDirectory, isMove);
                        succeededCount++;
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        CopyOrMoveDirectory(sourcePath, targetDirectory, isMove);
                        succeededCount++;
                    }
                    else
                    {
                        failures.Add(new SolutionFileOperationFailure(sourcePath, "源文件或文件夹不存在。"));
                    }
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or NotSupportedException)
                {
                    failures.Add(new SolutionFileOperationFailure(sourcePath, ex.Message));
                }
            }

            return new SolutionFileOperationResult(sources.Count, succeededCount, failures);
        }

        internal static bool IsSafeDirectoryCopyDestination(string sourcePath, string destinationPath)
        {
            return !IsSamePath(sourcePath, destinationPath)
                && !IsSubPathOf(destinationPath, sourcePath);
        }

        private static void CopyOrMoveFile(string sourcePath, string targetDirectory, bool isMove)
        {
            string destinationPath = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));
            if (!isMove)
            {
                File.Copy(sourcePath, GetAvailableCopyPath(destinationPath, isDirectory: false));
                return;
            }

            if (IsSamePath(sourcePath, destinationPath))
                throw new IOException("源文件已位于目标文件夹中。");
            if (PathExists(destinationPath))
                throw new IOException($"目标已存在：{destinationPath}");
            File.Move(sourcePath, destinationPath);
        }

        private static void CopyOrMoveDirectory(string sourcePath, string targetDirectory, bool isMove)
        {
            string destinationPath = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));
            if (!isMove)
            {
                if ((File.GetAttributes(sourcePath) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("不支持递归复制文件夹链接。");
                string copyDestination = GetAvailableCopyPath(destinationPath, isDirectory: true);
                if (!IsSafeDirectoryCopyDestination(sourcePath, copyDestination))
                    throw new IOException("不能将文件夹复制到自身或其子目录中。");
                try
                {
                    CopyDirectory(sourcePath, copyDestination);
                }
                catch
                {
                    try
                    {
                        if (Directory.Exists(copyDestination))
                            Directory.Delete(copyDestination, recursive: true);
                    }
                    catch
                    {
                    }
                    throw;
                }
                return;
            }

            if (IsSamePath(sourcePath, destinationPath))
                throw new IOException("源文件夹已位于目标文件夹中。");
            if (IsSubPathOf(destinationPath, sourcePath))
                throw new IOException("不能将文件夹移动到其子目录中。");
            if (PathExists(destinationPath))
                throw new IOException($"目标已存在：{destinationPath}");
            Directory.Move(sourcePath, destinationPath);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string filePath in Directory.EnumerateFiles(sourceDirectory))
            {
                File.Copy(filePath, Path.Combine(destinationDirectory, Path.GetFileName(filePath)));
            }
            foreach (DirectoryInfo directory in new DirectoryInfo(sourceDirectory).EnumerateDirectories())
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"文件夹包含不支持递归复制的链接：{directory.FullName}");
                CopyDirectory(
                    directory.FullName,
                    Path.Combine(destinationDirectory, directory.Name));
            }
        }

        private static string GetAvailableCopyPath(string desiredPath, bool isDirectory)
        {
            if (!PathExists(desiredPath))
                return desiredPath;

            string? directory = Path.GetDirectoryName(desiredPath);
            if (string.IsNullOrEmpty(directory))
                throw new IOException("无法确定复制目标文件夹。");

            string baseName = isDirectory
                ? Path.GetFileName(desiredPath)
                : Path.GetFileNameWithoutExtension(desiredPath);
            string extension = isDirectory ? string.Empty : Path.GetExtension(desiredPath);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = Path.GetFileName(desiredPath);
                extension = string.Empty;
            }

            for (int count = 1; ; count++)
            {
                string candidate = Path.Combine(directory, $"{baseName} - Copy ({count}){extension}");
                if (!PathExists(candidate))
                    return candidate;
            }
        }

        private static bool PathExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSubPathOf(string candidatePath, string parentPath)
        {
            string candidate = NormalizePath(candidatePath) + Path.DirectorySeparatorChar;
            string parent = NormalizePath(parentPath) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }
    }
}
