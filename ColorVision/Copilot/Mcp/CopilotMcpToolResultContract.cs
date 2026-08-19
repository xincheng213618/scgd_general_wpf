using System;

namespace ColorVision.Copilot.Mcp
{
    internal static class CopilotMcpToolResultContract
    {
        internal const string InvalidOutputFailureCode = "invalid_tool_output";

        public static CopilotMcpToolCallResult Capture(
            string expectedToolName,
            CopilotMcpToolCallResult? result)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedToolName);
            expectedToolName = expectedToolName.Trim();

            if (result == null)
                return Invalid(expectedToolName, "the handler returned null");
            if (!TryValidate(result, out var violation))
                return Invalid(expectedToolName, violation);

            return new CopilotMcpToolCallResult
            {
                Success = result.Success,
                Text = result.Text,
                ErrorCode = result.ErrorCode,
                FailureKind = result.FailureKind,
                RequiresApproval = result.RequiresApproval,
                ApprovalActionId = result.ApprovalActionId,
                ApprovalTitle = result.ApprovalTitle,
                ApprovalRiskLevel = result.ApprovalRiskLevel,
                ApprovalExpiresAtUtc = result.ApprovalExpiresAtUtc,
                ExecuteOnApproval = result.ExecuteOnApproval,
                ResumesAgentOnApproval = result.ResumesAgentOnApproval,
            };
        }

        private static bool TryValidate(
            CopilotMcpToolCallResult result,
            out string violation)
        {
            if (result.Text == null
                || result.ErrorCode == null
                || result.ApprovalActionId == null
                || result.ApprovalTitle == null
                || result.ApprovalRiskLevel == null)
            {
                return Fail("a required text field is null", out violation);
            }
            if (!Enum.IsDefined(result.FailureKind))
                return Fail("the failure kind is invalid", out violation);

            if (result.Success)
            {
                if (result.FailureKind != CopilotToolFailureKind.None
                    || !string.IsNullOrWhiteSpace(result.ErrorCode)
                    || result.RequiresApproval
                    || HasApprovalMetadata(result))
                {
                    return Fail("failure or approval metadata contradicts a successful result", out violation);
                }
            }
            else if (result.RequiresApproval)
            {
                if (!string.Equals(
                        result.ErrorCode,
                        "confirmation_required",
                        StringComparison.OrdinalIgnoreCase)
                    || result.FailureKind != CopilotToolFailureKind.None
                    || string.IsNullOrWhiteSpace(result.ApprovalActionId)
                    || string.IsNullOrWhiteSpace(result.ApprovalTitle)
                    || string.IsNullOrWhiteSpace(result.ApprovalRiskLevel)
                    || result.ApprovalExpiresAtUtc == default)
                {
                    return Fail("the approval metadata is incomplete or contradictory", out violation);
                }
            }
            else if (string.IsNullOrWhiteSpace(result.ErrorCode)
                || result.FailureKind == CopilotToolFailureKind.None
                || HasApprovalMetadata(result))
            {
                return Fail("failure metadata is incomplete or contradictory", out violation);
            }

            violation = string.Empty;
            return true;
        }

        private static bool HasApprovalMetadata(CopilotMcpToolCallResult result) =>
            !string.IsNullOrWhiteSpace(result.ApprovalActionId)
            || !string.IsNullOrWhiteSpace(result.ApprovalTitle)
            || !string.IsNullOrWhiteSpace(result.ApprovalRiskLevel)
            || result.ApprovalExpiresAtUtc != default
            || result.ExecuteOnApproval
            || result.ResumesAgentOnApproval;

        private static bool Fail(string violation, out string error)
        {
            error = violation;
            return false;
        }

        private static CopilotMcpToolCallResult Invalid(
            string toolName,
            string violation) =>
            CopilotMcpToolCallResult.Fail(
                InvalidOutputFailureCode,
                $"The MCP tool '{toolName}' returned invalid output: {violation}.",
                CopilotToolFailureKind.Internal);
    }
}
