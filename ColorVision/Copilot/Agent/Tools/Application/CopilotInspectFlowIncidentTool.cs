namespace ColorVision.Copilot
{
    public sealed class CopilotInspectFlowIncidentTool : CopilotFlowReadToolBase
    {
        public CopilotInspectFlowIncidentTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectFlowIncidentTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(CopilotSharedCapabilityCatalog.FlowIncident.AgentToolName, CopilotSharedCapabilityCatalog.FlowIncident.McpToolName, capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            request != null && request.Mode != CopilotAgentMode.Chat;
    }
}
