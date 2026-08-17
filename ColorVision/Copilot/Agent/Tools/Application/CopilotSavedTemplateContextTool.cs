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
                "Read the exact saved template attached with @ as a bounded, redacted, read-only in-memory snapshot. Use the template_code and template_name from that reference before describing its values. This never queries the database, modifies, or saves a template.",
                CopilotSharedCapabilityCatalog.SavedTemplateContext.SharedInputSchema!,
                capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsSavedTemplateContext(request);
    }
}
