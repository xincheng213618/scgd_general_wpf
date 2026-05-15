using System;
using System.Collections.Generic;
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

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = (toolInput?.Query ?? string.Empty).Trim();

            var snapshot = CopilotRecentLogSupport.Capture(query, CopilotRecentLogMode.RecentLines, MaxLogLines, MaxLogChars);
            if (!snapshot.Success)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = snapshot.Summary,
                    ErrorMessage = snapshot.ErrorMessage,
                });
            }

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = snapshot.Summary,
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"[日志文件] {snapshot.FilePath}",
                    snapshot.Content,
                }),
            });
        }
    }
}