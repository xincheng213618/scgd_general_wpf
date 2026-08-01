using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorVision.UI;

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

        public static bool NeedsPublicWebSearch(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || ExplicitlyDisallowsPublicWebAccess(request))
                return false;

            return request.Mode == CopilotAgentMode.Web
                || CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0
                || ContainsAny(request.UserText, PublicWebMarkers);
        }

        public static bool NeedsUrlFetch(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || ExplicitlyDisallowsPublicWebAccess(request))
                return false;

            return request.Mode == CopilotAgentMode.Web
                || CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0;
        }

        public static bool NeedsFlowGraph(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request))
                return false;

            var activeRequest = request!;
            if (NeedsShellExecution(activeRequest) && !ContainsAny(activeRequest.UserText, FlowGraphMarkers))
                return false;

            if (MatchesCurrentOrContinuation(activeRequest, FlowGraphMarkers,
                "InspectFlowGraph", "SearchFlowNodeCatalog", "PreviewFlowPatch", "ApplyFlowPatch"))
            {
                return true;
            }

            if (NeedsLocalEvidence(activeRequest))
                return false;

            return HasFlowContext(activeRequest)
                && (ContainsAny(activeRequest.UserText, CurrentSurfaceReferenceMarkers)
                    || ContainsAny(activeRequest.UserText, CurrentSurfaceProblemMarkers)
                        && !ContainsAny(activeRequest.UserText, DefinitionQuestionMarkers));
        }

        public static bool NeedsSavedTemplateContext(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (HasSavedTemplateContext(request!)
                    || MatchesCurrentOrContinuation(
                        request!,
                        SavedTemplateContextMarkers,
                        "InspectSavedTemplate"));
        }

        public static bool NeedsTemplateTypeContext(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (HasTemplateTypeContext(request!)
                    || MatchesCurrentOrContinuation(
                        request!,
                        TemplateTypeContextMarkers,
                        "InspectTemplateType"));
        }

        public static bool NeedsFlowMutation(CopilotAgentRequest? request)
        {
            return NeedsFlowGraph(request)
                && !ExplicitlyDisallowsWriteAccess(request)
                && ContainsAny(request!.UserText, FlowMutationMarkers)
                && !ContainsAny(request.UserText, MutationExplanationMarkers);
        }

        public static bool NeedsFlowExecutionStatistics(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && MatchesCurrentOrContinuation(request!, FlowStatisticsMarkers, "QueryFlowExecutionStats");
        }

        public static bool NeedsDatabaseRead(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && !ContainsAny(request!.UserText, DatabaseExplanationMarkers)
                && MatchesCurrentOrContinuation(request, DatabaseMarkers, "QueryDatabaseSql", "ExecuteDatabaseSql");
        }

        public static bool NeedsDatabaseWrite(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && !ExplicitlyDisallowsWriteAccess(request)
                && ContainsAny(request!.UserText, DatabaseMarkers)
                && ContainsAny(request.UserText, DatabaseMutationMarkers)
                && !ContainsAny(request.UserText, MutationExplanationMarkers);
        }

        public static bool NeedsRecentLogs(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, RecentLogMarkers, "GetRecentLog"));
        }

        public static bool NeedsWindowsSystemInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, WindowsSystemMarkers, "InspectWindowsSystem"));
        }

        public static bool NeedsWindowsProcessInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, WindowsProcessMarkers, "InspectWindowsProcesses"));
        }

        public static bool NeedsWindowsServiceInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, WindowsServiceMarkers, "InspectWindowsServices"));
        }

        public static bool NeedsTcpPortInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, TcpPortMarkers, "InspectTcpPort"));
        }

        public static bool NeedsGitWorkingTreeInspection(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request))
                return false;

            var activeRequest = request!;
            var intentText = RemoveExplicitFilePaths(activeRequest);
            return activeRequest.Mode == CopilotAgentMode.Review
                || NeedsWorkspaceEdit(activeRequest)
                || NeedsWorkspaceCreate(activeRequest)
                || NeedsWorkspaceRollback(activeRequest)
                || ContainsAny(intentText, GitWorkingTreeMarkers)
                || ContainsAny(intentText, GitDiffMarkers);
        }

        public static bool NeedsGitDiffInspection(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request))
                return false;

            var activeRequest = request!;
            var intentText = RemoveExplicitFilePaths(activeRequest);
            return activeRequest.Mode == CopilotAgentMode.Review
                || NeedsWorkspaceEdit(activeRequest)
                || NeedsWorkspaceCreate(activeRequest)
                || NeedsWorkspaceRollback(activeRequest)
                || ContainsAny(intentText, GitDiffMarkers);
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

        public static bool NeedsTaskLedger(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request)
                || request!.Mode is CopilotAgentMode.Chat or CopilotAgentMode.Explain
                || string.IsNullOrWhiteSpace(request.UserText))
            {
                return false;
            }

            if (request.Mode == CopilotAgentMode.Plan)
                return true;

            if (request.Recovery != null || ContainsAny(request.UserText, ExplicitPlanningMarkers))
                return true;

            var actionCount = CountDistinctActionIntents(request);
            if (actionCount < 2)
                return false;

            var hasExplicitParts = ContainsAny(request.UserText, MultiPartTaskMarkers)
                || request.UserText.Contains('\n')
                || request.UserText.Contains("1.", StringComparison.Ordinal)
                || request.UserText.Contains("1、", StringComparison.Ordinal);
            return hasExplicitParts && request.UserText.Length >= 80
                || actionCount >= 3 && request.UserText.Length >= 180;
        }

        private static int CountDistinctActionIntents(CopilotAgentRequest request)
        {
            var actions = new[]
            {
                NeedsLocalEvidence(request),
                NeedsWorkspaceEdit(request) || NeedsWorkspaceCreate(request) || NeedsWorkspaceRollback(request),
                NeedsWorkspaceValidation(request),
                NeedsPublicWebSearch(request) || NeedsUrlFetch(request),
                NeedsFlowMutation(request),
                NeedsDatabaseRead(request) || NeedsDatabaseWrite(request),
                NeedsRecentLogs(request)
                    || NeedsWindowsSystemInspection(request)
                    || NeedsWindowsProcessInspection(request)
                    || NeedsWindowsServiceInspection(request)
                    || NeedsTcpPortInspection(request),
                NeedsShellExecution(request),
                NeedsBatchImageProcessing(request),
            };
            return actions.Count(value => value);
        }

        internal static bool ExplicitlyRequiresPublicWebSearch(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || ExplicitlyDisallowsPublicWebAccess(request))
                return false;

            return request.Mode == CopilotAgentMode.Web
                || ContainsAny(request.UserText, ExplicitPublicWebSearchMarkers);
        }

        internal static bool ExplicitlyDisallowsPublicWebAccess(CopilotAgentRequest? request)
        {
            return request != null && ContainsAny(request.UserText, PublicWebOptOutMarkers);
        }

        internal static bool ExplicitlyDisallowsWriteAccess(CopilotAgentRequest? request)
        {
            return request != null
                && (IsReadOnlyMode(request.Mode)
                    || ContainsAny(request.UserText, ExplicitReadOnlyRequestMarkers));
        }

        internal static bool IsReadOnlyMode(CopilotAgentMode mode)
        {
            return mode is CopilotAgentMode.Plan or CopilotAgentMode.Review or CopilotAgentMode.Diagnose;
        }

        internal static bool ExplicitlyRequiresDelegatedWorkspaceEvidence(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && ContainsAny(request!.UserText, DelegatedWorkspaceEvidenceMarkers)
                && ContainsAny(request.UserText, ParentWorkspaceEvidenceOptOutMarkers);
        }

        internal static bool IsDirectWorkspaceEvidenceTool(ICopilotTool? tool)
        {
            if (tool == null || tool is CopilotDelegateSubagentTool)
                return false;

            return IsDirectWorkspaceEvidenceIdentity(tool.Name, tool.Description);
        }

        private static bool IsDirectWorkspaceEvidenceIdentity(string? toolName, string? description)
        {
            if (DirectWorkspaceEvidenceToolNames.Contains(toolName ?? string.Empty))
                return true;

            return ContainsAny($"{toolName} {description}", ExternalLocalSearchMarkers);
        }

        internal static bool IsUrlFetchTool(ICopilotTool? tool)
        {
            if (tool == null)
                return false;

            if (string.Equals(tool.Name, "FetchUrl", StringComparison.OrdinalIgnoreCase))
                return true;

            return ContainsAny($"{tool.Name} {tool.Description}", ExternalUrlFetchMarkers);
        }

        internal static bool IsPublicWebSearchTool(ICopilotTool? tool)
        {
            if (tool == null)
                return false;

            if (string.Equals(tool.Name, "WebSearch", StringComparison.OrdinalIgnoreCase))
                return true;

            return ContainsAny($"{tool.Name} {tool.Description}", ExternalWebSearchMarkers);
        }

        internal static bool IsWorkspaceApplyTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "ApplyWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceRollbackTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RollbackWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceValidationTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RunWorkspaceValidation", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsShellExecutionTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RunShellCommand", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBackgroundShellExecutionTool(ICopilotTool? tool)
        {
            return string.Equals(
                tool?.Name,
                "StartBackgroundShellCommand",
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBatchImageProcessingTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "OpenBatchImageProcessing", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBatchImageConversionTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "ConvertBatchImages", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBatchImageFilePath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && BatchImageFileExtensions.Contains(Path.GetExtension(path));
        }

        public static bool CanExposeExternalTool(CopilotAgentRequest? request, string? toolName, string? description)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;
            if ((request.RequiresDelegatedWorkspaceEvidence || HasBoundedExplicitFileScope(request))
                && IsDirectWorkspaceEvidenceIdentity(toolName, description))
            {
                return false;
            }

            var identity = $"{toolName} {description}";
            if (ContainsAny(identity, ExternalWebSearchMarkers))
                return NeedsPublicWebSearch(request);
            if (ContainsAny(identity, ExternalUrlFetchMarkers))
                return NeedsUrlFetch(request);
            if (ContainsAny(identity, ExternalLocalSearchMarkers))
                return NeedsWorkspaceDiscovery(request);

            return true;
        }

        public static bool CanRetainForFollowUp(CopilotAgentRequest? request, ICopilotTool? tool)
        {
            if (request == null || tool == null || request.Mode != CopilotAgentMode.Auto)
                return false;
            if (request.History.Count == 0
                || string.IsNullOrWhiteSpace(request.UserText)
                || request.UserText.Length > MaximumFollowUpCharacters)
                return false;

            if (ContainsAny(request.UserText, NewTopicMarkers)
                || ContainsAnyEnglishWordForm(request.UserText, LocalScopeMarkers)
                || ContainsAnyEnglishWordForm(request.UserText, LocalArtifactMarkers)
                    && ContainsAnyEnglishWordForm(request.UserText, LocalInspectionMarkers, includeVerbForms: true))
                return false;
            var capability = tool.Capability;
            if (capability.Access != CopilotToolAccess.ReadOnly
                || capability.Idempotency != CopilotToolIdempotency.Idempotent
                || capability.ApprovalMode != CopilotToolApprovalMode.Never)
                return false;

            if (HasRecentCheckpointToolEvidence(request.SessionCheckpoint, tool.Name))
                return true;

            return IsWebEvidenceTool(tool)
                && (HasRecentCheckpointWebEvidence(request.SessionCheckpoint)
                    || HasVisibleWebEvidence(request.History));
        }

        private static bool HasRecentCheckpointToolEvidence(CopilotAgentSessionCheckpoint? checkpoint, string toolName)
        {
            if (checkpoint?.IsStructurallyValid() != true
                || string.IsNullOrWhiteSpace(toolName)
                || DateTimeOffset.UtcNow - checkpoint.UpdatedAtUtc > FollowUpToolLeaseDuration)
            {
                return false;
            }

            var previousStop = checkpoint.TaskEventJournal.Events
                .LastOrDefault(item => item.Type == CopilotAgentTaskEventType.RunStopped);
            if (previousStop == null)
                return false;

            return checkpoint.TaskEventJournal.Events.Any(item =>
                string.Equals(item.RunId, previousStop.RunId, StringComparison.Ordinal)
                && item.Type == CopilotAgentTaskEventType.ToolCompleted
                && string.Equals(item.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesCurrentOrContinuation(CopilotAgentRequest request, string[] markers, params string[] toolNames)
        {
            if (ContainsAny(request.UserText, markers))
                return true;
            if (!IsExplicitContinuation(request))
                return false;
            if ((request.History ?? Array.Empty<CopilotRequestMessage>())
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(VisibleHistoryEvidenceLimit)
                .Any(message => ContainsAny(message.Content, markers)))
            {
                return true;
            }
            return toolNames.Any(toolName => HasRecentCheckpointToolEvidence(request.SessionCheckpoint, toolName));
        }

        private static bool IsExplicitContinuation(CopilotAgentRequest request)
        {
            return request.History.Count > 0
                && request.UserText.Length <= MaximumFollowUpCharacters
                && ContainsAny(request.UserText, FollowUpMarkers)
                && !ContainsAny(request.UserText, NewTopicMarkers);
        }

        private static bool HasFlowContext(CopilotAgentRequest request)
        {
            return request.ContextItems.Any(item =>
                (item.Id ?? string.Empty).EndsWith(":flow", StringComparison.OrdinalIgnoreCase)
                || (item.Title ?? string.Empty).StartsWith("Flow context", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasSavedTemplateContext(CopilotAgentRequest request)
        {
            return CopilotReferenceContextSupport.HasReference(
                request,
                "composer-template:",
                "[ColorVision saved template reference]");
        }

        private static bool HasTemplateTypeContext(CopilotAgentRequest request)
        {
            return CopilotReferenceContextSupport.HasReference(
                request,
                "composer-template-type:",
                "[ColorVision template type reference]");
        }

        private static bool IsAgentRequest(CopilotAgentRequest? request)
        {
            return request != null && request.Mode != CopilotAgentMode.Chat;
        }

        private static bool HasRecentCheckpointWebEvidence(CopilotAgentSessionCheckpoint? checkpoint)
        {
            if (checkpoint?.IsStructurallyValid() != true
                || DateTimeOffset.UtcNow - checkpoint.UpdatedAtUtc > FollowUpToolLeaseDuration)
                return false;

            var previousStop = checkpoint.TaskEventJournal.Events
                .LastOrDefault(item => item.Type == CopilotAgentTaskEventType.RunStopped);
            if (previousStop == null)
                return false;

            return checkpoint.TaskEventJournal.Events.Any(item =>
                string.Equals(item.RunId, previousStop.RunId, StringComparison.Ordinal)
                && item.Type is CopilotAgentTaskEventType.ToolStarted or CopilotAgentTaskEventType.ToolCompleted
                && IsFollowUpWebToolIdentity(item.ToolName, string.Empty));
        }

        private static bool HasVisibleWebEvidence(IReadOnlyList<CopilotRequestMessage> history)
        {
            return (history ?? Array.Empty<CopilotRequestMessage>())
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(VisibleHistoryEvidenceLimit)
                .Any(message => CopilotWebPageToolSupport.ExtractHttpUrls(message.Content).Count > 0
                    || ContainsAny(message.Content, PublicWebMarkers));
        }

        internal static bool IsWebEvidenceTool(ICopilotTool? tool)
        {
            if (tool == null)
                return false;

            return (tool is CopilotDelegateSubagentTool delegatedTool
                    && delegatedTool.Role.ContextScope == CopilotSubagentContextScope.PublicWeb)
                || string.Equals(tool.Name, "DelegateScout", StringComparison.OrdinalIgnoreCase)
                || IsUrlFetchTool(tool)
                || IsPublicWebSearchTool(tool);
        }

        private static bool IsFollowUpWebToolIdentity(string? name, string? description)
        {
            if (FollowUpWebToolNames.Contains(name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                return true;
            if (CopilotSubagentRoleCatalog.Default.TryGetByToolName(name ?? string.Empty, out var role)
                && role?.ContextScope == CopilotSubagentContextScope.PublicWeb)
                return true;

            var identity = $"{name} {description}";
            return ContainsAny(identity, ExternalWebSearchMarkers)
                || ContainsAny(identity, ExternalUrlFetchMarkers);
        }


    }
}
