namespace ColorVision.Copilot;

public sealed class CopilotInspectFlowRunAttemptsTool : CopilotFlowReadToolBase
{
    public CopilotInspectFlowRunAttemptsTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }

    public CopilotInspectFlowRunAttemptsTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        : base(CopilotSharedCapabilityCatalog.FlowRunAttempts.AgentToolName, CopilotSharedCapabilityCatalog.FlowRunAttempts.McpToolName, capabilityInvoker) { }

    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
