using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotToolResultContract
    {
        internal const string InvalidOutputFailureCode = "invalid_tool_output";

        public static CopilotToolResult Capture(
            string expectedToolName,
            CopilotToolResult? result)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedToolName);
            expectedToolName = expectedToolName.Trim();

            try
            {
                if (!TryValidate(expectedToolName, result, out var violation))
                    return Invalid(expectedToolName, violation);
                return Snapshot(expectedToolName, result!);
            }
            catch
            {
                // A third-party result may expose a hostile IReadOnlyList. Do not
                // let enumeration failures escape the same terminal tool outcome.
                return Invalid(expectedToolName, "the result could not be snapshotted safely");
            }
        }

        private static bool TryValidate(
            string expectedToolName,
            CopilotToolResult? result,
            out string violation)
        {
            if (result == null)
                return Fail("the tool returned null", out violation);
            if (result.ToolName == null
                || !string.Equals(result.ToolName.Trim(), expectedToolName, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("the result identity does not match the registered tool", out violation);
            }
            if (result.Summary == null
                || result.Content == null
                || result.ErrorMessage == null
                || result.FailureCode == null
                || result.ProcessOperation == null
                || result.ObservationProgressSignature == null)
            {
                return Fail("a required text field is null", out violation);
            }
            if (!Enum.IsDefined(result.FailureKind))
                return Fail("the failure kind is invalid", out violation);
            if (result.SuggestedReadableLocalFilePaths == null
                || result.AttemptedLocalFilePaths == null
                || result.SuccessfullyReadLocalFilePaths == null
                || result.LocalFileReadScopes == null
                || result.BackgroundShellCommands == null)
            {
                return Fail("a required result collection is null", out violation);
            }
            if (result.SuggestedReadableLocalFilePaths.Any(path => path == null)
                || result.AttemptedLocalFilePaths.Any(path => path == null)
                || result.SuccessfullyReadLocalFilePaths.Any(path => path == null)
                || result.LocalFileReadScopes.Any(scope => scope == null)
                || result.BackgroundShellCommands.Any(command => !command.IsStructurallyValid()))
            {
                return Fail("a result collection contains an invalid item", out violation);
            }

            if (result.Success)
            {
                if (result.FailureKind != CopilotToolFailureKind.None
                    || !string.IsNullOrWhiteSpace(result.FailureCode)
                    || !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    return Fail("failure metadata contradicts a successful result", out violation);
                }
            }
            else if (result.Approval != null
                || result.FailureKind == CopilotToolFailureKind.None)
            {
                return Fail("terminal metadata contradicts a failed result", out violation);
            }

            if (result.Approval != null
                && (string.IsNullOrWhiteSpace(result.Approval.ActionId)
                    || string.IsNullOrWhiteSpace(result.Approval.Title)
                    || string.IsNullOrWhiteSpace(result.Approval.RiskLevel)
                    || result.Approval.ExpiresAtUtc == default))
            {
                return Fail("the approval metadata is incomplete", out violation);
            }

            var hasProcessEvidence = !string.IsNullOrWhiteSpace(result.ProcessOperation)
                || result.ProcessExitCode.HasValue
                || result.ProcessTimedOut;
            if (hasProcessEvidence
                && !CopilotToolProcessEvidence.TryNormalizeForResult(
                    expectedToolName,
                    result.Success,
                    result.FailureCode,
                    result.ProcessOperation,
                    result.ProcessExitCode,
                    result.ProcessTimedOut,
                    out _))
            {
                return Fail("the process evidence contradicts the tool outcome", out violation);
            }

            if (result.WorkspaceMutation != null
                && (result.WorkspaceMutation.Files == null
                    || result.WorkspaceMutation.Files.Any(file => file == null)))
            {
                return Fail("the workspace mutation snapshot is invalid", out violation);
            }
            if (result.DelegatedRunUsage != null
                && (result.DelegatedRunUsage.RoleId == null
                    || result.DelegatedRunUsage.AgentName == null
                    || result.DelegatedRunUsage.RunId == null
                    || result.DelegatedRunUsage.ResumeFromRunId == null
                    || result.DelegatedRunUsage.Model == null
                    || result.DelegatedRunUsage.ReasoningEffort == null
                    || !Enum.IsDefined(result.DelegatedRunUsage.StopReason)))
            {
                return Fail("the delegated run metadata is invalid", out violation);
            }
            if (result.DelegatedAnswer != null
                && (result.DelegatedAnswer.Text == null
                    || !Enum.IsDefined(result.DelegatedAnswer.StopReason)))
            {
                return Fail("the delegated answer metadata is invalid", out violation);
            }

            violation = string.Empty;
            return true;
        }

        private static CopilotToolResult Snapshot(
            string expectedToolName,
            CopilotToolResult result)
        {
            return new CopilotToolResult
            {
                ToolName = expectedToolName,
                Success = result.Success,
                Summary = result.Summary,
                Content = result.Content,
                ErrorMessage = result.ErrorMessage,
                FailureKind = result.FailureKind,
                FailureCode = result.Success
                    ? string.Empty
                    : CopilotToolFailureCode.Normalize(result.FailureCode),
                ProcessOperation = result.ProcessOperation,
                ProcessExitCode = result.ProcessExitCode,
                ProcessTimedOut = result.ProcessTimedOut,
                Approval = Snapshot(result.Approval),
                SuggestedReadableLocalFilePaths = Freeze(result.SuggestedReadableLocalFilePaths),
                AttemptedLocalFilePaths = Freeze(result.AttemptedLocalFilePaths),
                SuccessfullyReadLocalFilePaths = Freeze(result.SuccessfullyReadLocalFilePaths),
                LocalFileReadScopes = Freeze(result.LocalFileReadScopes),
                DelegatedRunUsage = result.DelegatedRunUsage,
                DelegatedAnswer = result.DelegatedAnswer,
                ObservationCanRepeat = result.ObservationCanRepeat,
                ObservationProgressSignature = result.ObservationProgressSignature,
                WorkspaceMutation = Snapshot(result.WorkspaceMutation),
                BackgroundShellCommands = Freeze(result.BackgroundShellCommands),
                SuppressModelOutput = result.SuppressModelOutput,
            };
        }

        private static CopilotToolApprovalInfo? Snapshot(CopilotToolApprovalInfo? approval)
        {
            return approval == null
                ? null
                : new CopilotToolApprovalInfo
                {
                    ActionId = approval.ActionId,
                    Title = approval.Title,
                    RiskLevel = approval.RiskLevel,
                    ExpiresAtUtc = approval.ExpiresAtUtc,
                    ExecuteOnApproval = approval.ExecuteOnApproval,
                    ResumesAgentOnApproval = approval.ResumesAgentOnApproval,
                };
        }

        private static CopilotWorkspaceMutationSnapshot? Snapshot(
            CopilotWorkspaceMutationSnapshot? mutation)
        {
            return mutation == null
                ? null
                : new CopilotWorkspaceMutationSnapshot(Freeze(mutation.Files));
        }

        private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
        {
            return values.Count == 0
                ? Array.Empty<T>()
                : Array.AsReadOnly(values.ToArray());
        }

        private static bool Fail(string violation, out string error)
        {
            error = violation;
            return false;
        }

        private static CopilotToolResult Invalid(string toolName, string violation)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                Summary = $"{toolName} returned invalid output.",
                ErrorMessage = $"The tool result violated the runtime output contract: {violation}.",
                FailureKind = CopilotToolFailureKind.Internal,
                FailureCode = InvalidOutputFailureCode,
            };
        }
    }
}
