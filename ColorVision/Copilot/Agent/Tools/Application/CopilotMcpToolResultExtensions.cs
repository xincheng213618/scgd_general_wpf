using ColorVision.Copilot.Mcp;

namespace ColorVision.Copilot
{
    internal static class CopilotMcpToolResultExtensions
    {
        public static CopilotToolApprovalInfo? ToApprovalInfo(this CopilotMcpToolCallResult result)
        {
            if (result == null || !result.RequiresApproval || string.IsNullOrWhiteSpace(result.ApprovalActionId))
                return null;

            return new CopilotToolApprovalInfo
            {
                ActionId = result.ApprovalActionId,
                Title = result.ApprovalTitle,
                RiskLevel = result.ApprovalRiskLevel,
                ExpiresAtUtc = result.ApprovalExpiresAtUtc,
                ExecuteOnApproval = result.ExecuteOnApproval,
            };
        }
    }
}
