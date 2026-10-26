namespace ColorVision.Copilot;

public sealed class CopilotInspectRecordedFlowTemplateTool : CopilotFlowReadToolBase
{
    public CopilotInspectRecordedFlowTemplateTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }

    public CopilotInspectRecordedFlowTemplateTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        : base(CopilotSharedCapabilityCatalog.RecordedFlowTemplate.AgentToolName, CopilotSharedCapabilityCatalog.RecordedFlowTemplate.McpToolName, capabilityInvoker) { }

    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
