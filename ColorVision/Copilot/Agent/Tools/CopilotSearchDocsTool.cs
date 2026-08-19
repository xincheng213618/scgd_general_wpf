using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchDocsTool : ICopilotTool
    {
        public string Name => CopilotSharedCapabilityCatalog.SearchDocs.AgentToolName;

        public string Description => CopilotSharedCapabilityCatalog.SearchDocs.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.SearchDocs.AgentCapability;

        public CopilotToolEvidenceMode EvidenceMode => Capability.EvidenceMode;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.SearchDocs.AgentInputSchema;

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
