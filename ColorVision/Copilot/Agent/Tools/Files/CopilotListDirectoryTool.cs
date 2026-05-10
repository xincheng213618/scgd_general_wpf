using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotListDirectoryTool : ICopilotTool
    {
        private const int MaxListedEntries = 60;

        public string Name => "ListDirectory";

        public string Description => "列出当前轮允许访问的本地文件夹内容，返回子文件和子目录。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.ReadableLocalDirectoryPaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat;
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var allowedDirectories = request.ReadableLocalDirectoryPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (allowedDirectories.Length == 0)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "当前轮没有可列出的本地文件夹。",
                    ErrorMessage = "未检测到当前轮允许访问的本地文件夹路径。",
                });
            }

            var selectedDirectory = NormalizePath(toolInput?.Path);
            if (!string.IsNullOrWhiteSpace(selectedDirectory)
                && !allowedDirectories.Contains(selectedDirectory, StringComparer.OrdinalIgnoreCase))
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "规划器选择了不在允许列表中的本地文件夹。",
                    ErrorMessage = $"规划器选择的文件夹不在当前允许访问列表中：{selectedDirectory}",
                });
            }

            var directoryPath = !string.IsNullOrWhiteSpace(selectedDirectory)
                ? selectedDirectory
                : allowedDirectories[0];

            if (!Directory.Exists(directoryPath))
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "目标文件夹不存在。",
                    ErrorMessage = $"目标文件夹不存在：{directoryPath}",
                });
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
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "列出目录失败。",
                    ErrorMessage = ex.Message,
                });
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

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"已列出 {GetDirectoryLabel(directoryPath)}，包含 {subDirectories.Length} 个子目录、{files.Length} 个文件。",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = files
                    .Where(CopilotWorkspaceSearchSupport.IsTextLikeFile)
                    .ToArray(),
            });
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