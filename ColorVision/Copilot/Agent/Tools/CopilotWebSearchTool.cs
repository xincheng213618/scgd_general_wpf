using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotWebSearchTool : ICopilotTool
    {
        public string Name => "WebSearch";

        public string Description => "Search the public web and return result titles, snippets, and URLs. Useful when local ColorVision context or documentation is missing and the question can be answered from public information.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null && request.Mode != CopilotAgentMode.Chat;
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = string.IsNullOrWhiteSpace(toolInput?.Query)
                ? request.UserText
                : toolInput.Query;
            var result = await CopilotWebSearchCapability.SearchAsync(query, cancellationToken);
            return result.ToCapabilityResult().ToToolResult(Name);
        }
    }
}
