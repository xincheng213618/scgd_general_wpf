using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGetRecentLogTool : ICopilotAgentDrivenTool
    {
        public string Name => CopilotSharedCapabilityCatalog.RecentLog.AgentToolName;

        public string Description => CopilotSharedCapabilityCatalog.RecentLog.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.RecentLog.AgentCapability;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.RecentLog.AgentInputSchema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsRecentLogs(request);

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = (toolInput?.Query ?? string.Empty).Trim();
            var maxLines = CopilotRecentLogSupport.NormalizeToolMaxLines(toolInput.GetInt32Argument("max_lines"));
            var result = await CopilotRecentLogCapability.CaptureAsync(
                query,
                CopilotRecentLogMode.RecentLines,
                maxLines,
                CopilotRecentLogSupport.DefaultMaxLogChars,
                cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}
