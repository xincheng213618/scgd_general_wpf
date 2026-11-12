namespace ColorVision.Copilot
{
    public sealed class CopilotInspectFlowRunHistoryTool : CopilotFlowReadToolBase
    {
        public CopilotInspectFlowRunHistoryTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectFlowRunHistoryTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(CopilotSharedCapabilityCatalog.FlowRunHistory.AgentToolName, CopilotSharedCapabilityCatalog.FlowRunHistory.McpToolName, capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) =>
            request != null && request.Mode != CopilotAgentMode.Chat;
    }
}
