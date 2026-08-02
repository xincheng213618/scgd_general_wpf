namespace ColorVision.Copilot.Mcp
{
    internal enum CopilotMcpCommandAction
    {
        Summary,
        Verbose,
        Invalid,
    }

    internal static class CopilotMcpCommand
    {
        public const string Usage = "用法：/mcp [verbose]";

        public static CopilotMcpCommandAction Resolve(string? arguments)
        {
            return (arguments ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "" => CopilotMcpCommandAction.Summary,
                "verbose" => CopilotMcpCommandAction.Verbose,
                _ => CopilotMcpCommandAction.Invalid,
            };
        }
    }
}
