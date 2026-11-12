namespace ColorVision.Copilot
{
    public sealed class CopilotListFlowIncidentsTool : CopilotFlowReadToolBase
    {
        public CopilotListFlowIncidentsTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotListFlowIncidentsTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(CopilotSharedCapabilityCatalog.FlowIncidents.AgentToolName, CopilotSharedCapabilityCatalog.FlowIncidents.McpToolName, capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            request != null && request.Mode != CopilotAgentMode.Chat;
    }
}
