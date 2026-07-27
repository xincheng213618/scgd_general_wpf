using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotAgentToolSurfaceMetrics(
        int RegisteredToolCount,
        int AvailableToolCount,
        int AvailableToolDefinitionCharacters,
        int HarnessInstructionCharacters)
    {
        internal static CopilotAgentToolSurfaceMetrics Capture(
            int registeredToolCount,
            IReadOnlyList<ICopilotTool> availableTools,
            string? harnessInstructions)
        {
            availableTools ??= Array.Empty<ICopilotTool>();
            long definitionCharacters = 0;
            foreach (var tool in availableTools)
            {
                if (tool == null)
                    continue;

                definitionCharacters += tool.Name?.Length ?? 0;
                definitionCharacters += tool.Description?.Length ?? 0;
                definitionCharacters += tool.InputSchema.JsonSchema.GetRawText().Length;
            }

            var availableToolCount = availableTools.Count;
            return new CopilotAgentToolSurfaceMetrics(
                Math.Max(availableToolCount, Math.Max(0, registeredToolCount)),
                availableToolCount,
                ClampToInt(definitionCharacters),
                Math.Max(0, harnessInstructions?.Length ?? 0));
        }

        private static int ClampToInt(long value)
        {
            return (int)Math.Clamp(value, 0, int.MaxValue);
        }
    }
}
