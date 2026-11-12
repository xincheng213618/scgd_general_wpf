namespace ColorVision.Copilot;

public sealed class CopilotListFlowRunsTool : CopilotFlowReadToolBase
{
    public CopilotListFlowRunsTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }

    public CopilotListFlowRunsTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        : base(CopilotSharedCapabilityCatalog.FlowRuns.AgentToolName, CopilotSharedCapabilityCatalog.FlowRuns.McpToolName, capabilityInvoker) { }

    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
