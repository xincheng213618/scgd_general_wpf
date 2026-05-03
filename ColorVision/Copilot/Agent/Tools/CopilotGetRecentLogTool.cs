using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGetRecentLogTool : ICopilotTool
    {
        private const int MaxLogLines = 300;
        private const int MaxLogChars = 20000;

        private static readonly string[] DiagnoseKeywords =
        {
            "报错",
            "异常",
            "失败",
            "日志",
            "跑不起来",
            "error",
            "exception",
            "fail",
            "failed",
        };

        public string Name => "GetRecentLog";

        public string Description => "读取应用最近日志，用于诊断失败或异常问题。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            if (request.Mode == CopilotAgentMode.Diagnose)
                return true;

            var text = request.UserText ?? string.Empty;
            return DiagnoseKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var latestLog = GetCandidateLogDirectories()
                .Where(Directory.Exists)
                .SelectMany(directory => Directory.GetFiles(directory, "*.txt", SearchOption.TopDirectoryOnly))
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestLog == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "未找到最近日志。",
                    ErrorMessage = "当前环境没有发现可读取的日志文件。",
                };
            }

            var lines = await File.ReadAllLinesAsync(latestLog.FullName, cancellationToken);
            var recentLines = lines.TakeLast(MaxLogLines).ToArray();
            var content = string.Join(Environment.NewLine, recentLines);

            if (content.Length > MaxLogChars)
                content = content[..MaxLogChars] + Environment.NewLine + $"...<内容已截断，仅保留最近 {MaxLogChars} 字符。>";

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"已读取最近日志：{latestLog.Name}，保留最近 {recentLines.Length} 行。",
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"[日志文件] {latestLog.FullName}",
                    content,
                }),
            };
        }

        private static IEnumerable<string> GetCandidateLogDirectories()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Log");
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        }
    }
}