using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectSavedTemplateTool : CopilotFlowReadToolBase
    {
        private static readonly CopilotToolInputSchema Schema = CreateSchema(
            new Dictionary<string, object?>
            {
                ["template_code"] = new
                {
                    type = "string",
                    description = "Exact template code supplied by the attached saved-template reference.",
                },
                ["template_name"] = new
                {
                    type = "string",
                    description = "Exact saved template name supplied by the attached saved-template reference.",
                },
            },
            "template_code",
            "template_name");

        public CopilotInspectSavedTemplateTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectSavedTemplateTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(
                "InspectSavedTemplate",
                "get_saved_template_context",
                "Read the exact saved template attached with @ as a bounded, redacted, read-only in-memory snapshot. Use the template_code and template_name from that reference before describing its values. This never queries the database, modifies, or saves a template.",
                Schema,
                capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsSavedTemplateContext(request);
    }
}
