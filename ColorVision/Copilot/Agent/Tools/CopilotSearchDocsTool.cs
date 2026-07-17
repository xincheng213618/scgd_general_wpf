using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchDocsTool : ICopilotTool
    {
        public string Name => "SearchDocs";

        public string Description => "Search the ColorVision online documentation index and return the most relevant snippets by section, page, and heading. Useful for software usage, menus, devices, plugins, developer guides, and architecture questions.";

        public CopilotToolEvidenceMode EvidenceMode => CopilotToolEvidenceMode.RedactedExcerpt;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Focused ColorVision documentation search terms.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            return request.Mode == CopilotAgentMode.Web
                || CopilotDocsCapability.HasDocumentationIntent(request.UserText);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = CopilotDocsCapability.ResolveQuery(request.UserText, toolInput?.Query);
            var result = await CopilotDocsCapability.SearchAsync(query, cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}
