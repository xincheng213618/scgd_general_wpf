using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectTemplateTypeTool : CopilotFlowReadToolBase
    {
        public CopilotInspectTemplateTypeTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectTemplateTypeTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(
                CopilotSharedCapabilityCatalog.TemplateTypeContext.AgentToolName,
                CopilotSharedCapabilityCatalog.TemplateTypeContext.McpToolName,
                capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsTemplateTypeContext(request);
    }
}
