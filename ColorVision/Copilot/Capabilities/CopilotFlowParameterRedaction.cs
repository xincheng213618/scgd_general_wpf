using ColorVision.Copilot.Mcp;
using ColorVision.UI;

namespace ColorVision.Copilot;

/// <summary>Keeps Flow parameter output policy consistent without changing the captured source values.</summary>
internal static class CopilotFlowParameterRedaction
{
    internal static string RedactValue(CopilotContextProperty property) => CopilotMcpAuditLogger.RedactArgument(property.PropertyName,
        CopilotMcpAuditLogger.RedactArgument(property.Name, property.Value));
}
