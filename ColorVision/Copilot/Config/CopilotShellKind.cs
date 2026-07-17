namespace ColorVision.Copilot
{
    public enum CopilotShellKind
    {
        Auto,
        PowerShell,
        CommandPrompt,
    }

    public sealed class CopilotShellOption
    {
        public string Label { get; init; } = string.Empty;

        public CopilotShellKind Value { get; init; }
    }
}
