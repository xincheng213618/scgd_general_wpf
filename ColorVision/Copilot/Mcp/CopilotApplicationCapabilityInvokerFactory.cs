namespace ColorVision.Copilot
{
    internal static class CopilotApplicationCapabilityInvokerFactory
    {
        private static readonly Mcp.CopilotMcpToolDispatcher DefaultDispatcher = new();

        public static ICopilotApplicationCapabilityInvoker CreateDefault() => DefaultDispatcher;

        internal static Mcp.CopilotMcpToolDispatcher GetDefaultDispatcher() => DefaultDispatcher;
    }
}
