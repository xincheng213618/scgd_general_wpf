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
                || AsksToEditWorkspace(request)
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

        internal static bool CanUseWorkspaceEvidence(CopilotAgentRequest? request) =>
            NeedsLocalEvidence(request)
            || CanUseLocalReadTools(request)
                && (request!.WritableLocalRootPaths.Count > 0 || request.WritableLocalFilePaths.Count > 0);

        internal static bool CanDiscoverWorkspace(CopilotAgentRequest? request) =>
            CanUseWorkspaceEvidence(request) && !HasBoundedExplicitFileScope(request);

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

        internal static bool CanUseWorkspacePatch(CopilotAgentRequest? request)
        {
            // Tool availability follows the frozen scope; intent markers still drive execution requirements.
            return IsAgentRequest(request)
                && !string.IsNullOrWhiteSpace(request!.UserText)
                && !ExplicitlyDisallowsWriteAccess(request)
                && (request.WritableLocalRootPaths.Count > 0 || request.WritableLocalFilePaths.Count > 0)
                && !OptsOutOfEditing(request.UserText);
        }

        // "修复这个 bug，接口不要改" still asks for an edit, so a bare "不要改" counts only as the whole
        // closing clause; phrases naming the code or files count anywhere.
        private static bool OptsOutOfEditing(string? text)
        {
            if (ContainsAny(text, WorkspaceEditOptOutMarkers))
                return true;
            var trimmed = (text ?? string.Empty).TrimEnd(ClosingPunctuation);
            var lastClause = trimmed[(trimmed.LastIndexOfAny(ClauseSeparators) + 1)..].Trim();
            return EditOptOutClauses.Contains(lastClause, StringComparer.Ordinal);
        }

        public static bool NeedsWorkspaceEdit(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || ExplicitlyDisallowsWriteAccess(request)
                || request.WritableLocalRootPaths.Count == 0 && request.WritableLocalFilePaths.Count == 0
                || OptsOutOfEditing(request.UserText))
            {
                return false;
            }

            return AsksToEditWorkspace(request);
        }

        private static bool AsksToEditWorkspace(CopilotAgentRequest request)
        {
            var asksToEdit = ContainsAny(request.UserText, WorkspaceEditMarkers)
                || ContainsAny(request.UserText, CasualWorkspaceEditMarkers)
                    && (ContainsAny(request.UserText, LocalArtifactMarkers) || ContainsAny(request.UserText, CodeOnlyTargetMarkers))
                    && !ContainsAny(request.UserText, QuestionMarkers)
                    && !ContainsAny(request.UserText, NonFileEditTargetMarkers)
                    && !ContainsAny(request.UserText, DatabaseMarkers)
                    && !ContainsAny(request.UserText, FlowGraphMarkers);
            return asksToEdit
                && !ContainsAny(request.UserText, WorkspaceEditExplanationMarkers)
                && !RewritesConversationText(request);
        }

        private static bool RewritesConversationText(CopilotAgentRequest request)
        {
            return ContainsAny(request.UserText, ProseRewriteTargetMarkers)
                && request.ReadableLocalFilePaths.Count == 0
                && request.WritableLocalFilePaths.Count == 0
                && !ContainsAny(request.UserText, LocalArtifactMarkers);
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
                || OptsOutOfEditing(request.UserText)
                || ContainsAny(request.UserText, WorkspaceCreateExplanationMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceCreateMarkers)
                || ContainsAny(request.UserText, CasualWorkspaceCreateMarkers)
                    && ContainsAny(request.UserText, CasualWorkspaceCreateTargetMarkers)
                    && !ContainsAny(request.UserText, QuestionMarkers)
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

            var explicitlyRequestsValidation = ContainsAny(request.UserText, WorkspaceValidationMarkers)
                || ContainsAny(request.UserText, ColloquialWorkspaceValidationMarkers)
                    && !ContainsAny(request.UserText, ConceptualQuestionMarkers);
            if (request.Mode == CopilotAgentMode.Review)
                return explicitlyRequestsValidation;

            return explicitlyRequestsValidation && !ExplicitlyDisallowsWriteAccess(request);
        }

        public static bool NeedsShellExecution(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request)
                || ExplicitlyDisallowsWriteAccess(request)
                || CopilotProjectInitialization.IsInitializationRequest(request!.UserText)
                || ContainsAny(request!.UserText, ShellExplanationMarkers))
                return false;

            return ContainsAny(request.UserText, ShellMarkers)
                || !ContainsAny(request.UserText, ConceptualQuestionMarkers)
                    && (ContainsAny(request.UserText, BackgroundShellStartMarkers)
                        || request.UserText.Contains("命令", StringComparison.Ordinal)
                            && ContainsAny(request.UserText, ShellCommandVerbMarkers))
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
                && !ContainsAny(request.UserText, BatchImageAlgorithmAdvisoryMarkers)
                && !IsBatchImageAdvisoryQuestion(request.UserText)
                && !(ContainsAny(request.UserText, ScriptRuntimeMarkers)
                    && ContainsAny(request.UserText, ScriptExecutionMarkers))
                && ContainsAny(request.UserText, BatchImageMarkers)
                && (ContainsAny(request.UserText, BatchImageActionMarkers)
                    || IsBatchImageAlgorithmMutation(request.UserText));
        }

        public static bool NeedsBatchImageConversionExecution(CopilotAgentRequest? request)
        {
            return NeedsBatchImageProcessing(request)
                && (ContainsAny(request!.UserText, BatchImageConversionMarkers)
                    || IsBatchImageAlgorithmMutation(request.UserText));
        }

        // "能把这些图片转成 PNG 吗" is a polite request; only asking whether the work is needed is advice.
        private static bool IsBatchImageAdvisoryQuestion(string? text)
        {
            return ContainsAny(text, BatchImageAdvisoryQuestionMarkers)
                && (text ?? string.Empty).TrimEnd(ClosingPunctuation).EndsWith('吗');
        }

        private static bool IsBatchImageAlgorithmMutation(string text)
        {
            if (ContainsAny(text, ConceptualQuestionMarkers)
                || ContainsAny(text, BatchImageAlgorithmAdvisoryMarkers)
                || ContainsAny(text, BatchImageAlgorithmNegationMarkers)
                || ContainsAny(text, BatchImageAlgorithmReadOnlyStateMarkers)
                || !ContainsAny(text, BatchImageAlgorithmNameMarkers)
                || !ContainsAny(text, BatchImageAlgorithmTargetMarkers))
            {
                return false;
            }

            return ContainsAny(text, BatchImageAlgorithmExecutionMarkers)
                || ContainsAny(text, BatchImageAlgorithmChineseCommandMarkers)
                || ContainsAnyEnglishWordForm(text, BatchImageAlgorithmEnglishCommandMarkers);
        }
    }
}
