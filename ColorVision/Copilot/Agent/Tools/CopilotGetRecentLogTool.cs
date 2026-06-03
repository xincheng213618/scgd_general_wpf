using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGetRecentLogTool : ICopilotTool
    {
        private const int MaxLogLines = 300;
        private const int MaxLogChars = 20000;

        public string Name => "GetRecentLog";

        public string Description => "Read recent application logs for failure or exception diagnosis.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            return CopilotRecentLogCapability.HasAvailableLogFile();
        }

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
