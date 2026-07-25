using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectTemplateTypeTool : CopilotFlowReadToolBase
    {
        private static readonly CopilotToolInputSchema Schema = CreateSchema(
            new Dictionary<string, object?>
            {
                ["template_code"] = new
                {
                    type = "string",
                    description = "Exact template code supplied by the attached template-type reference.",
                },
            },
            "template_code");

        public CopilotInspectTemplateTypeTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectTemplateTypeTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(
                "InspectTemplateType",
                "get_template_type_context",
                "Inspect the template type attached with @ as bounded read-only metadata: identity, loaded saved names, and browsable parameter field schema without values. Use its exact template_code. This never queries the database, reads template values, modifies, or saves a template.",
                Schema,
                capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsTemplateTypeContext(request);
    }
}
