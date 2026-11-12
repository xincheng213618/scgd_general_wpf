namespace ColorVision.Copilot;

public sealed class CopilotInspectLoadedBatchResultsTool : CopilotFlowReadToolBase
{
    public CopilotInspectLoadedBatchResultsTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }
    public CopilotInspectLoadedBatchResultsTool(ICopilotApplicationCapabilityInvoker invoker)
        : base(CopilotSharedCapabilityCatalog.LoadedBatchResults.AgentToolName, CopilotSharedCapabilityCatalog.LoadedBatchResults.McpToolName, invoker) { }
    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
