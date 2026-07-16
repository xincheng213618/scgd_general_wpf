namespace ColorVision.Copilot
{
    internal static class CopilotApplicationCapabilityInvokerFactory
    {
        public static ICopilotApplicationCapabilityInvoker CreateDefault() => new Mcp.CopilotMcpToolDispatcher();
    }
}
