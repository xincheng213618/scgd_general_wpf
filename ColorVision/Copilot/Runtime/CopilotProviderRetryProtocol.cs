using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotProviderRetryProtocol
    {
        public static void Validate(CopilotProviderRetryInfo retry)
        {
            ArgumentNullException.ThrowIfNull(retry);
            if (retry.FailedAttempt < 1
                || retry.NextAttempt != retry.FailedAttempt + 1
                || retry.MaximumAttempts < retry.NextAttempt
                || retry.Delay < TimeSpan.Zero
                || string.IsNullOrWhiteSpace(retry.FailureKind)
                || retry.FailureKind.Length > 96
                || retry.FailureKind.Any(char.IsControl)
                || retry.StatusCode is < 100 or > 599
                || !string.Equals(
                    retry.RequestId,
                    CopilotProviderRequestId.Normalize(retry.RequestId),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Copilot provider retry event has invalid metadata.");
            }
        }

        public static void ValidateDiagnostic(
            CopilotProviderRetryInfo retry,
            string diagnosticText)
        {
            Validate(retry);
            if (!string.Equals(
                diagnosticText,
                retry.ToDiagnosticText(),
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Copilot Agent provider retry diagnostic has mismatched metadata.");
            }
        }
    }
}
