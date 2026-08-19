using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotToolProcessEvidenceSnapshot(
        string Operation,
        int? ExitCode,
        bool TimedOut);

    internal static class CopilotToolProcessEvidence
    {
        public const string ShellOperation = "shell";
        public const string BuildOperation = "build";
        public const string TestOperation = "test";

        public static bool IsSupportedTool(string? toolName)
        {
            return toolName is "RunShellCommand" or "RunWorkspaceValidation";
        }

        public static bool TryNormalize(
            string? toolName,
            string? operation,
            int? exitCode,
            bool timedOut,
            out CopilotToolProcessEvidenceSnapshot evidence)
        {
            evidence = new CopilotToolProcessEvidenceSnapshot(string.Empty, null, false);
            var normalizedOperation = (operation ?? string.Empty).Trim().ToLowerInvariant();
            var operationIsValid = toolName switch
            {
                "RunShellCommand" => normalizedOperation == ShellOperation,
                "RunWorkspaceValidation" => normalizedOperation is BuildOperation or TestOperation,
                _ => false,
            };
            if (!operationIsValid
                || (!timedOut && !exitCode.HasValue))
            {
                return false;
            }

            evidence = new CopilotToolProcessEvidenceSnapshot(
                normalizedOperation,
                exitCode,
                timedOut);
            return true;
        }

        public static bool TryNormalizeForExecution(
            string? toolName,
            CopilotToolExecutionState state,
            string? failureCode,
            string? operation,
            int? exitCode,
            bool timedOut,
            out CopilotToolProcessEvidenceSnapshot evidence)
        {
            if (!TryNormalize(
                    toolName,
                    operation,
                    exitCode,
                    timedOut,
                    out evidence))
            {
                return false;
            }

            var normalizedFailureCode = CopilotToolFailureCode.Normalize(failureCode);
            bool isConsistent;
            if (evidence.TimedOut)
            {
                isConsistent = (state is CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut)
                    && string.Equals(
                        normalizedFailureCode,
                        GetExpectedTimeoutFailureCode(toolName),
                        StringComparison.Ordinal);
            }
            else if (evidence.ExitCode == 0)
            {
                isConsistent = state == CopilotToolExecutionState.Completed
                    && normalizedFailureCode.Length == 0;
            }
            else
            {
                isConsistent = state == CopilotToolExecutionState.Failed
                    && string.Equals(
                        normalizedFailureCode,
                        GetExpectedNonzeroExitFailureCode(toolName),
                        StringComparison.Ordinal);
            }

            if (!isConsistent)
                evidence = new CopilotToolProcessEvidenceSnapshot(string.Empty, null, false);
            return isConsistent;
        }

        public static bool TryNormalizeForResult(
            string? toolName,
            bool success,
            string? failureCode,
            string? operation,
            int? exitCode,
            bool timedOut,
            out CopilotToolProcessEvidenceSnapshot evidence)
        {
            if (!TryNormalize(
                    toolName,
                    operation,
                    exitCode,
                    timedOut,
                    out evidence))
            {
                return false;
            }

            var normalizedFailureCode = CopilotToolFailureCode.Normalize(failureCode);
            bool isConsistent;
            if (evidence.TimedOut)
            {
                isConsistent = !success
                    && string.Equals(
                        normalizedFailureCode,
                        GetExpectedTimeoutFailureCode(toolName),
                        StringComparison.Ordinal);
            }
            else if (evidence.ExitCode == 0)
            {
                isConsistent = success && normalizedFailureCode.Length == 0;
            }
            else
            {
                isConsistent = !success
                    && string.Equals(
                        normalizedFailureCode,
                        GetExpectedNonzeroExitFailureCode(toolName),
                        StringComparison.Ordinal);
            }

            if (!isConsistent)
                evidence = new CopilotToolProcessEvidenceSnapshot(string.Empty, null, false);
            return isConsistent;
        }

        private static string GetExpectedTimeoutFailureCode(string? toolName)
        {
            return toolName switch
            {
                "RunShellCommand" => CopilotShellCommandService.TimedOutFailureCode,
                "RunWorkspaceValidation" => CopilotWorkspaceValidationService.ValidationTimedOutFailureCode,
                _ => string.Empty,
            };
        }

        private static string GetExpectedNonzeroExitFailureCode(string? toolName)
        {
            return toolName switch
            {
                "RunShellCommand" => CopilotShellCommandService.NonzeroExitFailureCode,
                "RunWorkspaceValidation" => CopilotWorkspaceValidationService.ValidationFailedFailureCode,
                _ => string.Empty,
            };
        }
    }
}
