namespace ColorVision.Copilot;

public sealed class CopilotValidateFlowGraphTool : CopilotFlowReadToolBase
{
    public CopilotValidateFlowGraphTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }

    public CopilotValidateFlowGraphTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        : base(CopilotSharedCapabilityCatalog.FlowGraphValidation.AgentToolName,
            CopilotSharedCapabilityCatalog.FlowGraphValidation.McpToolName, capabilityInvoker) { }

    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
