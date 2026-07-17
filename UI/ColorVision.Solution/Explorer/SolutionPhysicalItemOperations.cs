using System.IO;

namespace ColorVision.Solution.Explorer
{
    internal sealed record SolutionPhysicalItemFailure(string SourcePath, string Message);

    internal sealed class SolutionPhysicalItemResult
    {
        public int RequestedCount { get; }
        public IReadOnlyList<string> SuccessfulPaths { get; }
        public IReadOnlyList<string> ChangedPaths { get; }
        public IReadOnlyList<string> NewlyCreatedPaths { get; }
        public IReadOnlyList<SolutionPhysicalItemFailure> Failures { get; }
        public bool IsComplete => RequestedCount > 0 && SuccessfulPaths.Count == RequestedCount;

        public SolutionPhysicalItemResult(
            int requestedCount,
            IReadOnlyList<string> successfulPaths,
            IReadOnlyList<string> changedPaths,
            IReadOnlyList<string> newlyCreatedPaths,
            IReadOnlyList<SolutionPhysicalItemFailure> failures)
        {
            RequestedCount = requestedCount;
            SuccessfulPaths = successfulPaths;
            ChangedPaths = changedPaths;
            NewlyCreatedPaths = newlyCreatedPaths;
            Failures = failures;
        }
    }

    /// <summary>
    /// Creates and imports physical solution items with atomic destination writes.
    /// Tree, cache, and project-model synchronization remain the container's job.
    /// </summary>
    internal static class SolutionPhysicalItemOperations
    {
        public static SolutionPhysicalItemResult CreateFromTemplate(
            INewItemTemplate template,
            string targetDirectory,
            string fileName,
            bool overwrite)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (!TryGetTargetPath(targetDirectory, fileName, out string targetPath, out string errorMessage))
                return Failure(fileName, errorMessage);
            if (Directory.Exists(targetPath))
                return Failure(targetPath, "同名文件夹已经存在。");

            bool existed = File.Exists(targetPath);
            if (existed && !overwrite)
                return Failure(targetPath, "目标文件已经存在。");

            string temporaryPath = CreateTemporaryPath(targetDirectory, fileName);
            try
            {
                string? content = template.GetDefaultContent(fileName);
                if (content != null)
                    File.WriteAllText(temporaryPath, content);
                else
                    File.Create(temporaryPath).Dispose();
                File.Move(temporaryPath, targetPath, overwrite);
                return Success(targetPath, changed: true, newlyCreated: !existed);
            }
            catch (Exception ex)
            {
                return Failure(targetPath, ex.Message);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        public static SolutionPhysicalItemResult ImportFiles(
            IEnumerable<string> sourcePaths,
            string targetDirectory,
            bool overwrite)
        {
            ArgumentNullException.ThrowIfNull(sourcePaths);
            List<string> sources = sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var successfulPaths = new List<string>();
            var changedPaths = new List<string>();
            var newlyCreatedPaths = new List<string>();
            var failures = new List<SolutionPhysicalItemFailure>();
            var destinationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(targetDirectory))
            {
                failures.AddRange(sources.Select(path => new SolutionPhysicalItemFailure(
                    path,
                    "目标文件夹不存在。")));
                return new SolutionPhysicalItemResult(sources.Count, [], [], [], failures);
            }

            foreach (string sourcePath in sources)
            {
                string fullSourcePath;
                try
                {
                    fullSourcePath = Path.GetFullPath(sourcePath);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    failures.Add(new SolutionPhysicalItemFailure(sourcePath, ex.Message));
                    continue;
                }

                if (!File.Exists(fullSourcePath))
                {
                    failures.Add(new SolutionPhysicalItemFailure(fullSourcePath, "源文件不存在。"));
                    continue;
                }
                if (IsSolutionMetadataFile(fullSourcePath))
                {
                    failures.Add(new SolutionPhysicalItemFailure(
                        fullSourcePath,
                        "解决方案或项目文件必须使用对应的打开/添加工程命令。"));
                    continue;
                }

                string fileName = Path.GetFileName(fullSourcePath);
                if (!destinationNames.Add(fileName))
                {
                    failures.Add(new SolutionPhysicalItemFailure(
                        fullSourcePath,
                        $"本次选择中包含多个同名文件：{fileName}"));
                    continue;
                }

                string destinationPath = Path.Combine(targetDirectory, fileName);
                if (PathsEqual(fullSourcePath, destinationPath))
                {
                    successfulPaths.Add(destinationPath);
                    continue;
                }
                if (Directory.Exists(destinationPath))
                {
                    failures.Add(new SolutionPhysicalItemFailure(fullSourcePath, "目标位置存在同名文件夹。"));
                    continue;
                }

                bool existed = File.Exists(destinationPath);
                if (existed && !overwrite)
                {
                    failures.Add(new SolutionPhysicalItemFailure(fullSourcePath, "目标文件已经存在。"));
                    continue;
                }

                string temporaryPath = CreateTemporaryPath(targetDirectory, fileName);
                try
                {
                    File.Copy(fullSourcePath, temporaryPath, overwrite: false);
                    File.Move(temporaryPath, destinationPath, overwrite);
                    successfulPaths.Add(destinationPath);
                    changedPaths.Add(destinationPath);
                    if (!existed)
                        newlyCreatedPaths.Add(destinationPath);
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or NotSupportedException)
                {
                    failures.Add(new SolutionPhysicalItemFailure(fullSourcePath, ex.Message));
                }
                finally
                {
                    TryDeleteTemporaryFile(temporaryPath);
                }
            }

            return new SolutionPhysicalItemResult(
                sources.Count,
                successfulPaths,
                changedPaths,
                newlyCreatedPaths,
                failures);
        }

        public static IReadOnlyList<string> GetImportConflictPaths(
            IEnumerable<string> sourcePaths,
            string targetDirectory)
        {
            ArgumentNullException.ThrowIfNull(sourcePaths);
            if (!Directory.Exists(targetDirectory))
                return [];

            var conflicts = new List<string>();
            foreach (string sourcePath in sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                try
                {
                    string fullSourcePath = Path.GetFullPath(sourcePath);
                    if (!File.Exists(fullSourcePath) || IsSolutionMetadataFile(fullSourcePath))
                        continue;
                    string destinationPath = Path.Combine(targetDirectory, Path.GetFileName(fullSourcePath));
                    if (!PathsEqual(fullSourcePath, destinationPath) && File.Exists(destinationPath))
                        conflicts.Add(destinationPath);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                }
            }
            return conflicts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string GetAvailableFileName(
            INewItemTemplate template,
            string targetDirectory)
        {
            if (!TryGetAvailableFileName(template, targetDirectory, out string fileName, out string errorMessage))
                throw new InvalidOperationException(errorMessage);
            return fileName;
        }

        public static bool TryGetAvailableFileName(
            INewItemTemplate template,
            string targetDirectory,
            out string fileName,
            out string errorMessage)
        {
            ArgumentNullException.ThrowIfNull(template);
            fileName = string.Empty;
            errorMessage = string.Empty;
            try
            {
                string baseName = template.GetDefaultFileName() ?? template.Name;
                string extension = template.Extension ?? string.Empty;
                int count = 1;
                while (true)
                {
                    string candidate = count == 1
                        ? baseName + extension
                        : $"{baseName}({count}){extension}";
                    if (!TryGetTargetPath(targetDirectory, candidate, out string targetPath, out errorMessage))
                        return false;
                    if (!PathExists(targetPath))
                    {
                        fileName = candidate;
                        return true;
                    }
                    count = count == 1 ? 2 : count + 1;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"读取新建项模板失败：{ex.Message}";
                return false;
            }
        }

        public static string BuildFailureMessage(SolutionPhysicalItemResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            string details = string.Join(
                Environment.NewLine,
                result.Failures.Take(5).Select(failure =>
                    $"• {Path.GetFileName(failure.SourcePath)}: {failure.Message}"));
            if (result.Failures.Count > 5)
                details += $"{Environment.NewLine}• …";
            return $"已完成 {result.SuccessfulPaths.Count}/{result.RequestedCount} 项。{Environment.NewLine}{Environment.NewLine}{details}";
        }

        private static bool TryGetTargetPath(
            string targetDirectory,
            string fileName,
            out string targetPath,
            out string errorMessage)
        {
            targetPath = string.Empty;
            errorMessage = string.Empty;
            if (!Directory.Exists(targetDirectory))
            {
                errorMessage = "目标文件夹不存在。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                errorMessage = "文件名不允许为空。";
                return false;
            }

            string normalizedName = fileName.Trim();
            if (!string.Equals(normalizedName, Path.GetFileName(normalizedName), StringComparison.Ordinal)
                || normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                errorMessage = "文件名包含无效字符或路径分隔符。";
                return false;
            }

            try
            {
                targetPath = Path.Combine(targetDirectory, normalizedName);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool IsSolutionMetadataFile(string path)
        {
            return SolutionManager.IsSolutionFilePath(path)
                || ProjectProviderRegistry.IsSupportedProjectFilePath(path);
        }

        private static SolutionPhysicalItemResult Success(
            string path,
            bool changed,
            bool newlyCreated)
        {
            return new SolutionPhysicalItemResult(
                1,
                [path],
                changed ? [path] : [],
                newlyCreated ? [path] : [],
                []);
        }

        private static SolutionPhysicalItemResult Failure(string sourcePath, string message)
        {
            return new SolutionPhysicalItemResult(
                1,
                [],
                [],
                [],
                [new SolutionPhysicalItemFailure(sourcePath, message)]);
        }

        private static string CreateTemporaryPath(string targetDirectory, string fileName)
        {
            return Path.Combine(targetDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
            }
        }

        private static bool PathExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
