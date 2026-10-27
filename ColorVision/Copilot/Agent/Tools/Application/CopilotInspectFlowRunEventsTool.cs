namespace ColorVision.Copilot;

public sealed class CopilotInspectFlowRunEventsTool : CopilotFlowReadToolBase
{
    public CopilotInspectFlowRunEventsTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }

    public CopilotInspectFlowRunEventsTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        : base(CopilotSharedCapabilityCatalog.FlowRunEvents.AgentToolName,
            CopilotSharedCapabilityCatalog.FlowRunEvents.McpToolName, capabilityInvoker) { }

    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
