using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadLocalFileTool : ICopilotTool
    {
        private const int MaxFilesPerRequest = 3;

        public string Name => "ReadLocalFile";

        public string Description => "读取当前轮允许访问的本地文本文件，支持按路径和行范围精读。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.ReadableLocalFilePaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat;
        }

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var toolInput = request.SelectedToolInput ?? CopilotAgentToolInput.Empty;

            var allowedPaths = request.ReadableLocalFilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => NormalizePath(path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var selectedPath = NormalizePath(toolInput.Path);
            string[] paths;

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                if (!allowedPaths.Contains(selectedPath, StringComparer.OrdinalIgnoreCase))
                {
                    return new CopilotToolResult
                    {
                        ToolName = Name,
                        Success = false,
                        Summary = "规划器选择了不在允许列表中的本地文件。",
                        ErrorMessage = $"规划器选择的路径不在当前允许读取列表中：{selectedPath}",
                    };
                }

                paths = new[] { selectedPath };
            }
            else
            {
                paths = allowedPaths
                    .Take(MaxFilesPerRequest)
                    .ToArray();
            }

            if (paths.Length == 0)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
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

                var useSelectedRange = !string.IsNullOrWhiteSpace(selectedPath) && string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase);
                var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(
                    path,
                    useSelectedRange ? toolInput.StartLine : null,
                    useSelectedRange ? toolInput.EndLine : null,
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

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? BuildSuccessSummary(paths.Length, selectedPath, lastSuccess)
                    : $"未能读取本地文件，共 {paths.Length} 个路径。",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("；", errors),
            };
        }

        private static string BuildSuccessSummary(int pathCount, string selectedPath, CopilotLocalFileReadResult? lastSuccess)
        {
            if (!string.IsNullOrWhiteSpace(selectedPath) && lastSuccess.HasValue)
            {
                var result = lastSuccess.Value;
                if (result.StartLine > 0)
                    return $"已读取 {System.IO.Path.GetFileName(result.FullPath)} 第 {result.StartLine}-{result.EndLine} 行。";

                return $"已读取 {System.IO.Path.GetFileName(result.FullPath)}。";
            }

            return $"已读取 {Math.Min(pathCount, MaxFilesPerRequest)}/{pathCount} 个本地文件。";
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}