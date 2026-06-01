using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public static class CopilotReadLocalFileCapability
    {
        private const int DefaultMaxFilesPerRequest = 3;

        public static async Task<CopilotCapabilityResult> ReadAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? selectedPath,
            bool preferBatchReadAll,
            int? startLine,
            int? endLine,
            CancellationToken cancellationToken)
        {
            var allowedPaths = (readableLocalFilePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var normalizedSelectedPath = NormalizePath(selectedPath);
            var preferBatchRead = preferBatchReadAll && string.IsNullOrWhiteSpace(normalizedSelectedPath);
            string[] paths;

            if (!string.IsNullOrWhiteSpace(normalizedSelectedPath))
            {
                if (!allowedPaths.Contains(normalizedSelectedPath, StringComparer.OrdinalIgnoreCase))
                {
                    return new CopilotCapabilityResult
                    {
                        Success = false,
                        Summary = "规划器选择了不在允许列表中的本地文件。",
                        ErrorMessage = $"规划器选择的路径不在当前允许读取列表中：{normalizedSelectedPath}",
                    };
                }

                paths = new[] { normalizedSelectedPath };
            }
            else
            {
                paths = preferBatchRead
                    ? allowedPaths
                    : allowedPaths.Take(DefaultMaxFilesPerRequest).ToArray();
            }

            if (paths.Length == 0)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "当前轮没有可读取的本地文件路径。",
                    ErrorMessage = "未检测到当前轮允许访问的本地文件路径。",
                };
            }

            var builder = new StringBuilder();
            var successCount = 0;
            var errors = new List<string>();
            CopilotLocalFileReadResult? lastSuccess = null;

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var useSelectedRange = !string.IsNullOrWhiteSpace(normalizedSelectedPath)
                    && string.Equals(path, normalizedSelectedPath, StringComparison.OrdinalIgnoreCase);
                var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(
                    path,
                    useSelectedRange ? startLine : null,
                    useSelectedRange ? endLine : null,
                    cancellationToken);
                builder.AppendLine($"[文件] {result.FullPath}");

                if (result.Success)
                {
                    if (result.StartLine > 0)
                        builder.AppendLine($"[行] {result.StartLine}-{result.EndLine}");

                    if (result.WasTruncated)
                        builder.AppendLine("说明：文件内容较长，已截断后发送给模型。");

                    builder.AppendLine(result.Content);
                    successCount++;
                    lastSuccess = result;
                }
                else
                {
                    builder.AppendLine(result.ErrorMessage);
                    errors.Add($"{result.FullPath}: {result.ErrorMessage}");
                }

                builder.AppendLine();
            }

            return new CopilotCapabilityResult
            {
                Success = successCount > 0,
                Summary = successCount > 0
                    ? BuildSuccessSummary(successCount, paths.Length, normalizedSelectedPath, lastSuccess)
                    : $"未能读取本地文件，共 {paths.Length} 个路径。",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("；", errors),
            };
        }

        private static string BuildSuccessSummary(int successCount, int pathCount, string selectedPath, CopilotLocalFileReadResult? lastSuccess)
        {
            if (!string.IsNullOrWhiteSpace(selectedPath) && lastSuccess.HasValue)
            {
                var result = lastSuccess.Value;
                if (result.StartLine > 0)
                    return $"已读取 {Path.GetFileName(result.FullPath)} 第 {result.StartLine}-{result.EndLine} 行。";

                return $"已读取 {Path.GetFileName(result.FullPath)}。";
            }

            return $"已读取 {successCount}/{pathCount} 个本地文件。";
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }
    }

    public static class CopilotListDirectoryCapability
    {
        private const int MaxListedEntries = 60;

        public static CopilotCapabilityResult List(
            IEnumerable<string> readableLocalDirectoryPaths,
            string? selectedPath,
            CancellationToken cancellationToken)
        {
            var allowedDirectories = (readableLocalDirectoryPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (allowedDirectories.Length == 0)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "当前轮没有可列出的本地文件夹。",
                    ErrorMessage = "未检测到当前轮允许访问的本地文件夹路径。",
                };
            }

            var selectedDirectory = NormalizePath(selectedPath);
            if (!string.IsNullOrWhiteSpace(selectedDirectory)
                && !allowedDirectories.Contains(selectedDirectory, StringComparer.OrdinalIgnoreCase))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "规划器选择了不在允许列表中的本地文件夹。",
                    ErrorMessage = $"规划器选择的文件夹不在当前允许访问列表中：{selectedDirectory}",
                };
            }

            var directoryPath = !string.IsNullOrWhiteSpace(selectedDirectory)
                ? selectedDirectory
                : allowedDirectories[0];

            if (!Directory.Exists(directoryPath))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "目标文件夹不存在。",
                    ErrorMessage = $"目标文件夹不存在：{directoryPath}",
                };
            }

            string[] subDirectories;
            string[] files;
            try
            {
                subDirectories = Directory.EnumerateDirectories(directoryPath)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                files = Directory.EnumerateFiles(directoryPath)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "列出目录失败。",
                    ErrorMessage = ex.Message,
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            var builder = new StringBuilder();
            builder.AppendLine($"[文件夹] {directoryPath}");
            builder.AppendLine($"[子目录数] {subDirectories.Length}");
            builder.AppendLine($"[文件数] {files.Length}");
            builder.AppendLine();

            var listedCount = 0;
            foreach (var subDirectory in subDirectories)
            {
                if (listedCount >= MaxListedEntries)
                    break;

                builder.Append("[目录] ")
                    .AppendLine(Path.GetFileName(subDirectory));
                listedCount++;
            }

            foreach (var file in files)
            {
                if (listedCount >= MaxListedEntries)
                    break;

                builder.Append("[文件] ")
                    .AppendLine(Path.GetFileName(file));
                listedCount++;
            }

            if (subDirectories.Length + files.Length > listedCount)
            {
                builder.AppendLine();
                builder.AppendLine($"...<目录内容较多，仅展示前 {listedCount} 项。>");
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = $"已列出 {GetDirectoryLabel(directoryPath)}，包含 {subDirectories.Length} 个子目录、{files.Length} 个文件。",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = files
                    .Where(CopilotWorkspaceSearchSupport.IsTextLikeFile)
                    .ToArray(),
            };
        }

        private static string GetDirectoryLabel(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath);
            return string.IsNullOrWhiteSpace(name) ? directoryPath : name;
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}