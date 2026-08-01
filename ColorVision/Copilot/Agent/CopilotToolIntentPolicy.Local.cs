using System;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static partial class CopilotToolIntentPolicy
    {
        public static bool NeedsLocalEvidence(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            if (request.Mode is CopilotAgentMode.Diagnose or CopilotAgentMode.Review
                || request.ReadableLocalFilePaths.Count > 0
                || request.ReadableLocalDirectoryPaths.Count > 0)
            {
                return true;
            }

            if (ContainsAnyEnglishWordForm(request.UserText, LocalScopeMarkers)
                || ContainsAny(request.UserText, WorkspaceEditMarkers)
                    && !ContainsAny(request.UserText, WorkspaceEditExplanationMarkers)
                || ContainsAny(request.UserText, WorkspaceCreateMarkers)
                    && !ContainsAny(request.UserText, WorkspaceCreateExplanationMarkers)
                || ContainsAny(request.UserText, WorkspaceRollbackMarkers))
            {
                return true;
            }

            return ContainsAnyEnglishWordForm(request.UserText, LocalArtifactMarkers)
                && ContainsAnyEnglishWordForm(request.UserText, LocalInspectionMarkers, includeVerbForms: true)
                && !ContainsAny(request.UserText, ConceptualQuestionMarkers);
        }

        internal static bool NeedsWorkspaceDiscovery(CopilotAgentRequest? request)
        {
            return NeedsLocalEvidence(request) && !HasBoundedExplicitFileScope(request);
        }

        internal static bool HasBoundedExplicitFileScope(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request)
                || request!.ReadableLocalFilePaths.Count is < 1 or > 3
                || request.ReadableLocalDirectoryPaths.Count > 0
                || request.RequiresDelegatedWorkspaceEvidence
                || request.ReadableLocalFilePaths.Any(path => string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                || NeedsWorkspaceEdit(request)
                || NeedsWorkspaceCreate(request)
                || NeedsWorkspaceRollback(request)
                || NeedsWorkspaceValidation(request)
                || NeedsShellExecution(request)
                || NeedsBatchImageProcessing(request))
            {
                return false;
            }

            var intentText = RemoveExplicitFilePaths(request);
            return !ContainsAny(intentText, WorkspaceDiscoveryMarkers)
                && !ContainsAny(intentText, DelegatedWorkspaceEvidenceMarkers)
                && !ContainsAny(intentText, ExternalLocalSearchMarkers);
        }

        public static bool NeedsWorkspaceEdit(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || ExplicitlyDisallowsWriteAccess(request)
                || request.WritableLocalRootPaths.Count == 0 && request.WritableLocalFilePaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceEditOptOutMarkers)
                || ContainsAny(request.UserText, WorkspaceEditExplanationMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceEditMarkers);
        }

        public static bool NeedsWorkspaceRollback(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || ExplicitlyDisallowsWriteAccess(request)
                || request.WritableLocalRootPaths.Count == 0 && request.WritableLocalFilePaths.Count == 0)
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceRollbackMarkers);
        }

        public static bool NeedsWorkspaceCreate(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || ExplicitlyDisallowsWriteAccess(request)
                || request.WritableLocalRootPaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceEditOptOutMarkers)
                || ContainsAny(request.UserText, WorkspaceCreateExplanationMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceCreateMarkers)
                || ContainsAny(request.UserText, ScriptCreateActionMarkers)
                    && (ContainsAny(request.UserText, ScriptRuntimeMarkers)
                        || request.UserText.Contains("脚本", StringComparison.OrdinalIgnoreCase));
        }

        public static bool NeedsWorkspaceValidation(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.WritableLocalRootPaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceValidationExplanationMarkers))
            {
                return false;
            }

            var explicitlyRequestsValidation = ContainsAny(request.UserText, WorkspaceValidationMarkers);
            if (request.Mode == CopilotAgentMode.Review)
                return explicitlyRequestsValidation;

            return explicitlyRequestsValidation && !ExplicitlyDisallowsWriteAccess(request);
        }

        public static bool NeedsShellExecution(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request)
                || ExplicitlyDisallowsWriteAccess(request)
                || ContainsAny(request!.UserText, ShellExplanationMarkers))
                return false;

            return ContainsAny(request.UserText, ShellMarkers)
                || ContainsAny(request.UserText, ScriptRuntimeMarkers)
                    && ContainsAny(request.UserText, ScriptExecutionMarkers)
                || ContainsAny(request.UserText, ScriptExecutionMarkers)
                    && MatchesCurrentOrContinuation(request, ScriptRuntimeMarkers, "RunShellCommand")
                || ContainsAny(request.UserText, BatchAutomationMarkers)
                    && !NeedsBatchImageProcessing(request);
        }

        public static bool NeedsBackgroundShellExecution(CopilotAgentRequest? request)
        {
            return NeedsShellExecution(request)
                && MatchesCurrentOrContinuation(
                    request!,
                    BackgroundShellExecutionMarkers,
                    "StartBackgroundShellCommand");
        }

        public static bool NeedsBackgroundShellInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (NeedsBackgroundShellExecution(request)
                    || MatchesCurrentOrContinuation(
                        request!,
                        BackgroundShellInspectionMarkers,
                        "StartBackgroundShellCommand",
                        "InspectBackgroundShellCommands",
                        "ReadBackgroundShellCommandOutput",
                        "WaitForBackgroundShellCommand",
                        "WaitForBackgroundShellCommands"));
        }

        public static bool NeedsBackgroundShellStop(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && !ExplicitlyDisallowsWriteAccess(request)
                && MatchesCurrentOrContinuation(
                    request!,
                    BackgroundShellStopMarkers,
                    "StartBackgroundShellCommand",
                    "InspectBackgroundShellCommands",
                    "ReadBackgroundShellCommandOutput",
                    "WaitForBackgroundShellCommand",
                    "WaitForBackgroundShellCommands");
        }

        public static bool NeedsBatchImageProcessing(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && !ExplicitlyDisallowsWriteAccess(request)
                && !ContainsAny(request!.UserText, ConceptualQuestionMarkers)
                && !(ContainsAny(request.UserText, ScriptRuntimeMarkers)
                    && ContainsAny(request.UserText, ScriptExecutionMarkers))
                && ContainsAny(request.UserText, BatchImageMarkers)
                && ContainsAny(request.UserText, BatchImageActionMarkers);
        }

        public static bool NeedsBatchImageConversionExecution(CopilotAgentRequest? request)
        {
            return NeedsBatchImageProcessing(request)
                && ContainsAny(request!.UserText, BatchImageConversionMarkers);
        }
    }
}
