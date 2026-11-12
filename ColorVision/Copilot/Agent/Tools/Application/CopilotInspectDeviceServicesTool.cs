namespace ColorVision.Copilot;

public sealed class CopilotInspectDeviceServicesTool : CopilotFlowReadToolBase
{
    public CopilotInspectDeviceServicesTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }
    public CopilotInspectDeviceServicesTool(ICopilotApplicationCapabilityInvoker invoker)
        : base(CopilotSharedCapabilityCatalog.DeviceServices.AgentToolName, CopilotSharedCapabilityCatalog.DeviceServices.McpToolName, invoker) { }
    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
