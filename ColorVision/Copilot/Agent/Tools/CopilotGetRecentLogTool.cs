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

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Optional error text, exception name, or keyword used to filter recent logs.");

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            return CopilotToolIntentPolicy.NeedsRecentLogInspection(request)
                && CopilotRecentLogCapability.HasAvailableLogFile();
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
