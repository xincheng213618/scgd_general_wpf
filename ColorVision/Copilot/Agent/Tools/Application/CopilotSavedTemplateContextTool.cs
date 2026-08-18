using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectSavedTemplateTool : CopilotFlowReadToolBase
    {
        public CopilotInspectSavedTemplateTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectSavedTemplateTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(
                CopilotSharedCapabilityCatalog.SavedTemplateContext.AgentToolName,
                CopilotSharedCapabilityCatalog.SavedTemplateContext.McpToolName,
                capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsSavedTemplateContext(request);
    }
}
