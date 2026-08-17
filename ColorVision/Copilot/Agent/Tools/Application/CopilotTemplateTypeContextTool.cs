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
                "Inspect the template type attached with @ as bounded read-only metadata: identity, loaded saved names, and browsable parameter field schema without values. Use its exact template_code. This never queries the database, reads template values, modifies, or saves a template.",
                CopilotSharedCapabilityCatalog.TemplateTypeContext.SharedInputSchema!,
                capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsTemplateTypeContext(request);
    }
}
