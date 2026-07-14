using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGetRecentLogTool : ICopilotAgentDrivenTool
    {
        private const int MaxLogLines = 300;
        private const int MaxLogChars = 20000;

        public string Name => "GetRecentLog";

        public string Description => "Read recent ColorVision application logs for failure or exception diagnosis. Do not use this tool for Windows version, port, process, service, or other machine-state inspection.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Optional error text, exception name, or keyword used to filter recent logs.");

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = (toolInput?.Query ?? string.Empty).Trim();
            var result = CopilotRecentLogCapability.Capture(query, CopilotRecentLogMode.RecentLines, MaxLogLines, MaxLogChars);
            return Task.FromResult(result.ToToolResult(Name));
        }
    }
}
