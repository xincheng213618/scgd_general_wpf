namespace ColorVision.Copilot;

public sealed class CopilotReadAlgorithmHelpTool : CopilotFlowReadToolBase
{
    public CopilotReadAlgorithmHelpTool() : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault()) { }

    public CopilotReadAlgorithmHelpTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        : base(CopilotSharedCapabilityCatalog.AlgorithmHelp.AgentToolName,
            CopilotSharedCapabilityCatalog.AlgorithmHelp.McpToolName, capabilityInvoker) { }

    public override bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;
}
