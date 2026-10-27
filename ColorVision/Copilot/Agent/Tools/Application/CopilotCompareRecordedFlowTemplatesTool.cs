namespace ColorVision.Copilot;

public sealed class CopilotCompareRecordedFlowTemplatesTool : CopilotFlowReadToolBase
{
    public CopilotCompareRecordedFlowTemplatesTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }

    public CopilotCompareRecordedFlowTemplatesTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        : base(CopilotSharedCapabilityCatalog.RecordedFlowTemplateComparison.AgentToolName,
            CopilotSharedCapabilityCatalog.RecordedFlowTemplateComparison.McpToolName, capabilityInvoker) { }

    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
