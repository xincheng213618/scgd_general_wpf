#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.Copilot.Mcp;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Desktop.Feedback;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private void HandleAgentSkillsCommand(CopilotLocalCommand command, string arguments)
        {
            var request = CopilotAgentSkillCommand.Parse(arguments);
            if (request.Action == CopilotAgentSkillCommandAction.Invalid)
            {
                ShowLocalCommandResult(command, CopilotAgentSkillCommand.Usage);
                return;
            }

            var resultPrefix = string.Empty;
            if (request.Action is CopilotAgentSkillCommandAction.Disable or CopilotAgentSkillCommandAction.Enable)
            {
                var catalog = DiscoverAgentSkillCatalog(includeDisabled: true, forceReload: false);
                if (request.CatalogIndex > catalog.Count)
                {
                    ShowLocalCommandResult(
                        command,
                        $"Skill 编号超出范围；当前目录共有 {catalog.Count} 项。{Environment.NewLine}{CopilotAgentSkillCommand.Usage}");
                    return;
                }

                var item = catalog[request.CatalogIndex - 1];
                var disabled = request.Action == CopilotAgentSkillCommandAction.Disable;
                var changed = SetAgentSkillPathState(
                    item,
                    disabled ? CopilotAgentSkillOverrideState.Off : CopilotAgentSkillOverrideState.On);
                resultPrefix = changed
                    ? $"已按精确路径{(disabled ? "关闭" : "启用")} #{request.CatalogIndex} ${item.Name}；从下一次请求开始生效。"
                    : $"#{request.CatalogIndex} ${item.Name} 的精确路径已经处于请求状态。";
            }

            ShowLocalCommandResult(
                command,
                (resultPrefix.Length == 0 ? string.Empty : resultPrefix + Environment.NewLine + Environment.NewLine)
                + BuildAgentSkillDiagnosticsReport(request.Action == CopilotAgentSkillCommandAction.Reload));
        }

        private string BuildAgentSkillDiagnosticsReport(bool forceReload)
        {
            var agentDefaults = _config.AgentDefaults;
            var overrides = agentDefaults.CreateSkillOverrideSnapshot();
            var pathOverrides = agentDefaults.CreateSkillPathOverrideSnapshot();
            var availableSkills = DiscoverAgentSkillCatalog(includeDisabled: true, forceReload);
            return CopilotAgentSkillDiagnostics.FormatReport(
                CopilotAgentSkillUsageStore.Shared.GetSnapshot(),
                CopilotAgentSkills.ResolveMetadataCharacterBudget(agentDefaults.ContextWindowTokens),
                overrides,
                availableSkills,
                forceReload,
                pathOverrides);
        }

        private IReadOnlyList<CopilotAgentSkillCatalogItem> DiscoverAgentSkillCatalog(
            bool includeDisabled,
            bool forceReload)
        {
            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var trustedProjectRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(turnSnapshot);
            if (forceReload)
                CopilotAgentSkillCatalog.Invalidate();

            var agentDefaults = _config.AgentDefaults;
            return CopilotAgentSkillCatalog.DiscoverCached(
                    trustedProjectRoots,
                    includeDisabled ? null : agentDefaults.CreateSkillOverrideSnapshot(),
                    applicationBaseDirectory: null,
                    userProfileDirectory: null,
                    activeDocumentPath: turnSnapshot.ActiveDocumentPath,
                    pathOverrides: includeDisabled ? null : agentDefaults.CreateSkillPathOverrideSnapshot())
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private bool SetAgentSkillPathState(
            CopilotAgentSkillCatalogItem item,
            CopilotAgentSkillOverrideState state)
        {
            var agentDefaults = _config.AgentDefaults;
            var skillFilePath = CopilotAgentSkillOverrideConfig.NormalizeSkillFilePath(item.SkillFilePath);
            if (skillFilePath.Length == 0
                || state is not (CopilotAgentSkillOverrideState.On or CopilotAgentSkillOverrideState.Off))
                return false;

            var entries = CopilotAgentSkillOverrideConfig.Normalize(agentDefaults.SkillOverrides)
                .Where(entry => !string.Equals(entry.SkillFilePath, skillFilePath, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Clone())
                .ToList();
            entries.Add(new CopilotAgentSkillOverrideConfig
            {
                Name = item.Name,
                SkillFilePath = skillFilePath,
                State = state,
            });

            var normalized = CopilotAgentSkillOverrideConfig.Normalize(entries);
            var before = CopilotAgentSkillOverrideConfig.Normalize(agentDefaults.SkillOverrides);
            var changed = !before
                .Select(entry => (entry.Name, entry.SkillFilePath, entry.State))
                .SequenceEqual(normalized.Select(entry => (entry.Name, entry.SkillFilePath, entry.State)));
            if (!changed)
                return false;

            agentDefaults.SkillOverrides.Clear();
            foreach (var entry in normalized)
                agentDefaults.SkillOverrides.Add(entry);
            CopilotAgentSkillCatalog.Invalidate();
            PersistConfig();
            return true;
        }

        private string BuildPermissionDiagnosticsReport()
        {
            var mode = ResolveComposerRequestMode();
            var turnSnapshot = SelectedConversation == null
                ? CaptureHostedTurnSnapshot(Attachments)
                : CaptureHostedTurnSnapshot(
                    SelectedConversation,
                    attachmentOverride: Attachments);
            var requestPlan = CopilotAgentRequestFactory.Prepare(string.Empty, mode, turnSnapshot);
            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            return CopilotPermissionDiagnostics.Format(new CopilotPermissionDiagnosticSnapshot
            {
                Mode = mode,
                AccessMode = ComposerAccessMode,
                SearchRootPaths = requestPlan.SearchRootPaths,
                TrustedProjectRootPaths = requestPlan.TrustedProjectRootPaths,
                WritableRootPaths = requestPlan.WritableLocalRootPaths,
                WritableFilePaths = requestPlan.WritableLocalFilePaths,
                CapabilityCatalogRevision = capabilitySnapshot.Revision,
                Capabilities = capabilitySnapshot.Capabilities,
                ExternalMcpServers = _config.ExternalMcpServers,
                PendingApprovals = CopilotMcpConfirmationStore.Instance.PendingCount,
            });
        }

        private void HandlePermissionsCommand(CopilotLocalCommand command, string arguments)
        {
            switch (CopilotPermissionCommand.Resolve(arguments))
            {
                case CopilotPermissionCommandAction.OpenSelector:
                    DismissLocalCommandResult();
                    AccessModeSelectionRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CopilotPermissionCommandAction.ShowStatus:
                    ShowLocalCommandResult(command, BuildPermissionDiagnosticsReport());
                    break;
                case CopilotPermissionCommandAction.UseConfirmProtectedActions:
                    DismissLocalCommandResult();
                    SetComposerAccessMode(CopilotAgentAccessMode.ConfirmProtectedActions);
                    break;
                case CopilotPermissionCommandAction.UseTemporaryAutoReview:
                    DismissLocalCommandResult();
                    SetComposerAccessMode(CopilotAgentAccessMode.FullAccess);
                    break;
                default:
                    ShowLocalCommandResult(command, CopilotPermissionCommand.Usage);
                    break;
            }
        }

        private void HandleAdditionalDirectoryCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotAdditionalDirectoryCommand.Parse(arguments);
            var conversation = SelectedConversation ?? EnsureConversation();
            var currentPaths = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                conversation.AdditionalReadRootPaths);
            switch (request.Action)
            {
                case CopilotAdditionalDirectoryCommandAction.List:
                    ShowLocalCommandResult(
                        command,
                        CopilotAdditionalDirectoryCommand.Format(currentPaths));
                    return;
                case CopilotAdditionalDirectoryCommandAction.Clear:
                    if (!conversation.ReplaceAdditionalReadRootPaths(Array.Empty<string>()))
                    {
                        ShowLocalCommandResult(command, "当前会话没有附加只读目录。");
                        return;
                    }

                    UpdateConversationMetadata(conversation, touch: true);
                    PersistState(immediate: true);
                    ShowLocalCommandResult(
                        command,
                        "已清空当前会话的附加只读目录；后续 Agent 请求只使用工作区、活动文档、附件和请求中显式写出的路径。");
                    return;
                case CopilotAdditionalDirectoryCommandAction.Remove:
                    if (request.Ordinal > currentPaths.Length)
                    {
                        ShowLocalCommandResult(
                            command,
                            $"没有编号 {request.Ordinal:N0} 的附加目录。{Environment.NewLine}{Environment.NewLine}"
                            + CopilotAdditionalDirectoryCommand.Format(currentPaths));
                        return;
                    }

                    var removedPath = currentPaths[request.Ordinal - 1];
                    conversation.ReplaceAdditionalReadRootPaths(
                        currentPaths.Where((_, index) => index != request.Ordinal - 1));
                    UpdateConversationMetadata(conversation, touch: true);
                    PersistState(immediate: true);
                    ShowLocalCommandResult(
                        command,
                        $"已移除附加只读目录：{removedPath}{Environment.NewLine}{Environment.NewLine}"
                        + CopilotAdditionalDirectoryCommand.Format(conversation.AdditionalReadRootPaths));
                    return;
                case CopilotAdditionalDirectoryCommandAction.Add:
                    if (!CopilotAdditionalDirectoryCommand.TryNormalizeExistingDirectory(
                            request.Path,
                            out var addedPath,
                            out var errorMessage))
                    {
                        ShowLocalCommandResult(command, errorMessage);
                        return;
                    }

                    var workspaceRoot = CaptureHostedTurnSnapshot(
                        conversation.Attachments).SolutionDirectoryPath;
                    var workspaceRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots([workspaceRoot]);
                    if (CopilotWorkspaceSearchSupport.IsPathWithinRoots(addedPath, workspaceRoots))
                    {
                        ShowLocalCommandResult(
                            command,
                            "该目录已经位于当前工作区读取范围内，无需重复添加：" + addedPath);
                        return;
                    }
                    if (CopilotWorkspaceSearchSupport.IsPathWithinRoots(addedPath, currentPaths))
                    {
                        ShowLocalCommandResult(
                            command,
                            "该目录已经被现有附加目录覆盖：" + addedPath);
                        return;
                    }

                    var mergedPaths = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                        currentPaths.Append(addedPath));
                    if (!mergedPaths.Contains(addedPath, StringComparer.OrdinalIgnoreCase))
                    {
                        ShowLocalCommandResult(
                            command,
                            $"当前会话最多保留 {CopilotAdditionalDirectoryCommand.MaximumDirectories:N0} 个附加目录；请先使用 /add-dir remove N 移除一个。");
                        return;
                    }

                    conversation.ReplaceAdditionalReadRootPaths(mergedPaths);
                    UpdateConversationMetadata(conversation, touch: true);
                    PersistState(immediate: true);
                    ShowLocalCommandResult(
                        command,
                        $"已添加附加只读目录：{addedPath}{Environment.NewLine}"
                        + "它只对后续新 Agent 请求生效，不会扩大写入范围或加载其中的配置。"
                        + Environment.NewLine
                        + Environment.NewLine
                        + CopilotAdditionalDirectoryCommand.Format(conversation.AdditionalReadRootPaths));
                    return;
                default:
                    ShowLocalCommandResult(
                        command,
                        $"用法：{CopilotAdditionalDirectoryCommand.Usage}");
                    return;
            }
        }

        private string BuildHookDiagnosticsReport()
        {
            var extensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            return CopilotHookDiagnostics.Format(new CopilotHookDiagnosticSnapshot
            {
                HookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                    _currentCodexConfigOptions.ConfiguredHooksEnabled),
                ExtensionSources = extensionSnapshot.Sources,
                ExtensionIssues = extensionSnapshot.Issues,
                RecentToolExecutions = CopilotToolExecutionAuditLogger.GetRecentEntries(30),
            });
        }

        private void ShowLocalCommandResult(CopilotLocalCommand command, string report)
        {
            LocalCommandResultTitle = $"{command.Name} · 本地快照";
            LocalCommandResultText = report;
        }

        public void ShowKeyboardShortcutHelp()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/shortcuts");
            if (command != null)
                ShowLocalCommandResult(command, CopilotKeyboardShortcutHelp.Format());
        }

        private void ShowContextDiagnosticsFromUi()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/context");
            if (command != null)
                ShowLocalCommandResult(command, BuildContextDiagnosticsReport());
        }

        private void ShowUsageDiagnosticsFromUi()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/usage");
            if (command == null)
                return;

            ShowLocalCommandResult(
                command,
                CopilotUsageCommand.Format(
                    SelectedConversation,
                    Conversations,
                    DateTimeOffset.Now,
                    "session",
                    CopilotProviderRateLimitTracker.GetSnapshot(SelectedProfile?.Id)));
        }

        private string BuildContextDiagnosticsReport()
        {
            var mode = ResolveComposerRequestMode();
            var agentContextEnabled = mode != CopilotAgentMode.Chat;
            var selectedProfile = SelectedProfile;
            var conversation = SelectedConversation;
            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var projectInstructionOptions = turnSnapshot.ProjectInstructionDiscoveryOptions;
            var requestProfile = selectedProfile == null
                ? null
                : CreateConversationRequestProfile(selectedProfile, conversation, mode, projectInstructionOptions);
            var historyLimits = ResolveConversationHistoryLimits(
                requestProfile,
                projectInstructionOptions);
            var history = CopilotConversationRequestBuilder.CaptureHistorySelection(conversation, historyLimits);
            var projectInstructions = Array.Empty<CopilotProjectInstructionDocument>();
            var trustedProjectRoots = Array.Empty<string>();
            CopilotAgentSkillUsageSnapshot? skillUsage = null;
            if (agentContextEnabled)
            {
                trustedProjectRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(turnSnapshot).ToArray();
                projectInstructions = CopilotAgentProjectInstructions.DiscoverWithGlobal(
                    trustedProjectRoots,
                    turnSnapshot.ActiveDocumentPath,
                    turnSnapshot.Attachments
                        .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                        .Select(attachment => attachment.Value),
                    turnSnapshot.GlobalInstructionRootPath,
                    turnSnapshot.ProjectInstructionDiscoveryOptions)
                    .ToArray();
                skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            }

            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var agentExtensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            var toolHookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                projectInstructionOptions.ConfiguredHooksEnabled);
            var agentDefaults = _config.AgentDefaults;
            var retainedHistoryWeight = history.Messages.Sum(message => CopilotTokenEstimator.EstimateTextWeight(message.Content));
            var autoCompactionUsage = CopilotConversationAutoCompactionPolicy.Measure(
                conversation,
                historyLimits,
                InputText);
            var compaction = conversation?.Compaction;
            var personality = CopilotResponsePersonalitySelection.Resolve(
                conversation,
                projectInstructionOptions);
            var sleepPrevention = CopilotActiveTurnSleepPrevention.CaptureRuntimeSnapshot();
            return CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                ProfileLabel = requestProfile?.DisplayLabel ?? string.Empty,
                Mode = mode,
                ResponsePersonality = personality.Personality,
                ResponsePersonalitySourceLabel = personality.SourceLabel,
                CodexPersonalityEnabled = projectInstructionOptions.ConfiguredPersonalityEnabled,
                HasCodexPersonalityEnabledOverride = projectInstructionOptions.HasPersonalityEnabledOverride,
                CodexPersonalityEnabledSourceLabel = projectInstructionOptions.PersonalityEnabledSourceLabel,
                CodexWebSearchMode = projectInstructionOptions.ConfiguredWebSearchMode,
                CodexWebSearchModeSourceLabel = projectInstructionOptions.WebSearchModeSourceLabel,
                HasCodexWebSearchModeOverride = projectInstructionOptions.HasWebSearchModeOverride,
                CodexSandboxMode = projectInstructionOptions.ConfiguredSandboxMode,
                CodexSandboxModeSourceLabel = projectInstructionOptions.SandboxModeSourceLabel,
                HasCodexSandboxModeOverride = projectInstructionOptions.HasSandboxModeOverride,
                CodexApprovalPolicy = projectInstructionOptions.ConfiguredApprovalPolicy,
                CodexApprovalPolicySourceLabel = projectInstructionOptions.ApprovalPolicySourceLabel,
                HasCodexApprovalPolicyOverride = projectInstructionOptions.HasApprovalPolicyOverride,
                CodexApprovalsReviewer = projectInstructionOptions.ConfiguredApprovalsReviewer,
                CodexApprovalsReviewerSourceLabel = projectInstructionOptions.ApprovalsReviewerSourceLabel,
                HasCodexApprovalsReviewerOverride = projectInstructionOptions.HasApprovalsReviewerOverride,
                CodexGuardianApprovalEnabled = projectInstructionOptions.ConfiguredGuardianApprovalEnabled,
                CodexGuardianApprovalEnabledSourceLabel = projectInstructionOptions.GuardianApprovalEnabledSourceLabel,
                HasCodexGuardianApprovalEnabledOverride = projectInstructionOptions.HasGuardianApprovalEnabledOverride,
                CodexAutoReviewPolicyCharacters = projectInstructionOptions.ConfiguredAutoReviewPolicy.Length,
                CodexAutoReviewPolicySourceLabel = projectInstructionOptions.AutoReviewPolicySourceLabel,
                HasCodexAutoReviewPolicyOverride = projectInstructionOptions.HasAutoReviewPolicyOverride,
                CodexModel = projectInstructionOptions.ConfiguredModel,
                HasCodexModelOverride = projectInstructionOptions.HasModelOverride,
                CodexModelSourceLabel = projectInstructionOptions.ModelSourceLabel,
                CodexReviewModel = projectInstructionOptions.ConfiguredReviewModel,
                HasCodexReviewModelOverride = projectInstructionOptions.HasReviewModelOverride,
                CodexReviewModelSourceLabel = projectInstructionOptions.ReviewModelSourceLabel,
                CodexPreventIdleSleep = projectInstructionOptions.ConfiguredPreventIdleSleep,
                HasCodexPreventIdleSleepOverride = projectInstructionOptions.HasPreventIdleSleepOverride,
                CodexPreventIdleSleepSourceLabel = projectInstructionOptions.PreventIdleSleepSourceLabel,
                CodexShellToolEnabled = projectInstructionOptions.ConfiguredShellToolEnabled,
                HasCodexShellToolEnabledOverride = projectInstructionOptions.HasShellToolEnabledOverride,
                CodexShellToolEnabledSourceLabel = projectInstructionOptions.ShellToolEnabledSourceLabel,
                CodexHooksEnabled = projectInstructionOptions.ConfiguredHooksEnabled,
                HasCodexHooksEnabledOverride = projectInstructionOptions.HasHooksEnabledOverride,
                CodexHooksEnabledSourceLabel = projectInstructionOptions.HooksEnabledSourceLabel,
                CodexShellEnvironmentPolicySummary = projectInstructionOptions
                    .ConfiguredShellEnvironmentPolicy.BuildRedactedSummary(),
                HasCodexShellEnvironmentPolicyOverride = projectInstructionOptions.HasShellEnvironmentPolicyOverride,
                CodexShellEnvironmentPolicySourceLabel = projectInstructionOptions.ShellEnvironmentPolicySourceLabel,
                CodexShellEnvironmentPolicyError = projectInstructionOptions.ShellEnvironmentPolicyError,
                CodexGoalsEnabled = projectInstructionOptions.ConfiguredGoalsEnabled,
                HasCodexGoalsEnabledOverride = projectInstructionOptions.HasGoalsEnabledOverride,
                CodexGoalsEnabledSourceLabel = projectInstructionOptions.GoalsEnabledSourceLabel,
                CodexDefaultModeRequestUserInputEnabled = projectInstructionOptions.ConfiguredDefaultModeRequestUserInputEnabled,
                HasCodexDefaultModeRequestUserInputEnabledOverride = projectInstructionOptions.HasDefaultModeRequestUserInputEnabledOverride,
                CodexDefaultModeRequestUserInputEnabledSourceLabel = projectInstructionOptions.DefaultModeRequestUserInputEnabledSourceLabel,
                CodexExperimentalRequestUserInputEnabled = projectInstructionOptions.ConfiguredExperimentalRequestUserInputEnabled,
                HasCodexExperimentalRequestUserInputEnabledOverride = projectInstructionOptions.HasExperimentalRequestUserInputEnabledOverride,
                CodexExperimentalRequestUserInputEnabledSourceLabel = projectInstructionOptions.ExperimentalRequestUserInputEnabledSourceLabel,
                CodexUpdatePlanEnabled = projectInstructionOptions.ConfiguredUpdatePlanEnabled,
                HasCodexUpdatePlanEnabledOverride = projectInstructionOptions.HasUpdatePlanEnabledOverride,
                CodexUpdatePlanEnabledSourceLabel = projectInstructionOptions.UpdatePlanEnabledSourceLabel,
                CodexIncludePermissionsInstructions = projectInstructionOptions.ConfiguredIncludePermissionsInstructions,
                HasCodexIncludePermissionsInstructionsOverride = projectInstructionOptions.HasIncludePermissionsInstructionsOverride,
                CodexIncludePermissionsInstructionsSourceLabel = projectInstructionOptions.IncludePermissionsInstructionsSourceLabel,
                CodexIncludeCollaborationModeInstructions = projectInstructionOptions.ConfiguredIncludeCollaborationModeInstructions,
                HasCodexIncludeCollaborationModeInstructionsOverride = projectInstructionOptions.HasIncludeCollaborationModeInstructionsOverride,
                CodexIncludeCollaborationModeInstructionsSourceLabel = projectInstructionOptions.IncludeCollaborationModeInstructionsSourceLabel,
                CodexIncludeEnvironmentContext = projectInstructionOptions.ConfiguredIncludeEnvironmentContext,
                HasCodexIncludeEnvironmentContextOverride = projectInstructionOptions.HasIncludeEnvironmentContextOverride,
                CodexIncludeEnvironmentContextSourceLabel = projectInstructionOptions.IncludeEnvironmentContextSourceLabel,
                CodexIncludeSkillInstructions = projectInstructionOptions.ConfiguredIncludeSkillInstructions,
                HasCodexIncludeSkillInstructionsOverride = projectInstructionOptions.HasIncludeSkillInstructionsOverride,
                CodexIncludeSkillInstructionsSourceLabel = projectInstructionOptions.IncludeSkillInstructionsSourceLabel,
                CodexMultiAgentEnabled = projectInstructionOptions.ConfiguredMultiAgentEnabled,
                HasCodexMultiAgentEnabledOverride = projectInstructionOptions.HasMultiAgentEnabledOverride,
                CodexMultiAgentEnabledSourceLabel = projectInstructionOptions.MultiAgentEnabledSourceLabel,
                CodexAgentsEnabled = projectInstructionOptions.ConfiguredAgentsEnabled,
                HasCodexAgentsEnabledOverride = projectInstructionOptions.HasAgentsEnabledOverride,
                CodexAgentsEnabledSourceLabel = projectInstructionOptions.AgentsEnabledSourceLabel,
                CodexInterruptMessageEnabled = projectInstructionOptions.ConfiguredInterruptMessageEnabled,
                HasCodexInterruptMessageOverride = projectInstructionOptions.HasInterruptMessageOverride,
                CodexInterruptMessageSourceLabel = projectInstructionOptions.InterruptMessageSourceLabel,
                CodexMaximumConcurrentSubagentRuns = projectInstructionOptions.ConfiguredMaximumConcurrentSubagentRuns,
                HasCodexMaximumConcurrentSubagentRunsOverride = projectInstructionOptions.HasMaximumConcurrentSubagentRunsOverride,
                CodexMaximumConcurrentSubagentRunsSourceLabel = projectInstructionOptions.MaximumConcurrentSubagentRunsSourceLabel,
                CodexDefaultSubagentModel = projectInstructionOptions.ConfiguredDefaultSubagentModel,
                HasCodexDefaultSubagentModelOverride = projectInstructionOptions.HasDefaultSubagentModelOverride,
                CodexDefaultSubagentModelSourceLabel = projectInstructionOptions.DefaultSubagentModelSourceLabel,
                CodexDefaultSubagentReasoningEffort = projectInstructionOptions.ConfiguredDefaultSubagentReasoningEffort,
                HasCodexDefaultSubagentReasoningEffortOverride = projectInstructionOptions.HasDefaultSubagentReasoningEffortOverride,
                CodexDefaultSubagentReasoningEffortSourceLabel = projectInstructionOptions.DefaultSubagentReasoningEffortSourceLabel,
                ActiveSleepPreventionLeaseCount = sleepPrevention.ActiveLeaseCount,
                SleepPreventionLastErrorCode = sleepPrevention.LastErrorCode,
                SleepPreventionLastFailure = sleepPrevention.LastFailure,
                SystemPromptCharacters = requestProfile?.EffectiveSystemPrompt.Length ?? 0,
                ConfiguredModelInstructionsCharacters = projectInstructionOptions.ModelInstructions.Length,
                ConfiguredModelInstructionsSourceLabel = projectInstructionOptions.ModelInstructionsSourceLabel,
                HasConfiguredModelInstructionsOverride = projectInstructionOptions.HasModelInstructionsOverride,
                ConfiguredModelInstructionsUsesFile = projectInstructionOptions.ModelInstructionsUsesFile,
                ConfiguredModelInstructionsApplied = projectInstructionOptions.HasEffectiveModelInstructions
                    && selectedProfile?.HasSystemPromptOverride != true,
                SourceHistoryMessages = history.SourceMessageCount,
                RetainedHistoryMessages = history.Messages.Length,
                SourceHistoryCharacters = history.SourceCharacters,
                RetainedHistoryCharacters = history.RetainedCharacters,
                RetainedHistoryEstimatedTokens = history.Messages.Length == 0
                    ? 0
                    : CopilotTokenEstimator.WeightToTokenEstimate(retainedHistoryWeight),
                HistoryMaximumMessages = historyLimits.MaximumMessages,
                HistoryMaximumCharacters = historyLimits.MaximumCharacters,
                HistoryMaximumContentCharacters = historyLimits.MaximumContentCharacters,
                HistoryMaximumEstimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(historyLimits.MaximumCharacters),
                HistoryMaximumContentEstimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(historyLimits.MaximumContentCharacters),
                HistoryContextWindowTokens = ResolveContextWindowTokens(projectInstructionOptions),
                HasModelContextWindowOverride = projectInstructionOptions.HasModelContextWindowOverride,
                ModelContextWindowSourceLabel = projectInstructionOptions.ModelContextWindowSourceLabel,
                ToolOutputTokenLimit = projectInstructionOptions.ConfiguredToolOutputTokenLimit,
                HasToolOutputTokenLimitOverride = projectInstructionOptions.HasToolOutputTokenLimitOverride,
                ToolOutputTokenLimitSourceLabel = projectInstructionOptions.ToolOutputTokenLimitSourceLabel,
                CodexReasoningEffort = projectInstructionOptions.ConfiguredModelReasoningEffort,
                HasCodexReasoningEffortOverride = projectInstructionOptions.HasModelReasoningEffortOverride,
                CodexReasoningEffortSourceLabel = projectInstructionOptions.ModelReasoningEffortSourceLabel,
                CodexReasoningSummary = projectInstructionOptions.ConfiguredModelReasoningSummary,
                HasCodexReasoningSummaryOverride = projectInstructionOptions.HasModelReasoningSummaryOverride,
                CodexReasoningSummarySourceLabel = projectInstructionOptions.ModelReasoningSummarySourceLabel,
                CodexModelSupportsReasoningSummaries = projectInstructionOptions.ConfiguredModelSupportsReasoningSummaries,
                HasCodexModelSupportsReasoningSummariesOverride = projectInstructionOptions.HasModelSupportsReasoningSummariesOverride,
                CodexModelSupportsReasoningSummariesSourceLabel = projectInstructionOptions.ModelSupportsReasoningSummariesSourceLabel,
                CodexHideAgentReasoning = projectInstructionOptions.ConfiguredHideAgentReasoning,
                HasCodexHideAgentReasoningOverride = projectInstructionOptions.HasHideAgentReasoningOverride,
                CodexHideAgentReasoningSourceLabel = projectInstructionOptions.HideAgentReasoningSourceLabel,
                CodexFastModeEnabled = projectInstructionOptions.ConfiguredFastModeEnabled,
                HasCodexFastModeEnabledOverride = projectInstructionOptions.HasFastModeEnabledOverride,
                CodexFastModeEnabledSourceLabel = projectInstructionOptions.FastModeEnabledSourceLabel,
                CodexServiceTier = projectInstructionOptions.ConfiguredServiceTier,
                HasCodexServiceTierOverride = projectInstructionOptions.HasServiceTierOverride,
                CodexServiceTierSourceLabel = projectInstructionOptions.ServiceTierSourceLabel,
                CodexModelVerbosity = projectInstructionOptions.ConfiguredModelVerbosity,
                HasCodexModelVerbosityOverride = projectInstructionOptions.HasModelVerbosityOverride,
                CodexModelVerbositySourceLabel = projectInstructionOptions.ModelVerbositySourceLabel,
                AutoCompactConversationHistory = agentDefaults.AutoCompactConversationHistory,
                AutoCompactThresholdPercent = agentDefaults.AutoCompactThresholdPercent,
                ConfiguredModelAutoCompactTokenLimit = projectInstructionOptions.ConfiguredModelAutoCompactTokenLimit,
                HasModelAutoCompactTokenLimitOverride = projectInstructionOptions.HasModelAutoCompactTokenLimitOverride,
                ModelAutoCompactTokenLimitSourceLabel = projectInstructionOptions.ModelAutoCompactTokenLimitSourceLabel,
                ModelAutoCompactTokenLimitScope = projectInstructionOptions.EffectiveModelAutoCompactTokenLimitScope,
                HasModelAutoCompactTokenLimitScopeOverride = projectInstructionOptions.HasModelAutoCompactTokenLimitScopeOverride,
                ModelAutoCompactTokenLimitScopeSourceLabel = projectInstructionOptions.ModelAutoCompactTokenLimitScopeSourceLabel,
                AutoCompactTotalEstimatedTokens = EstimateContextTokens(autoCompactionUsage.ActiveWeight),
                AutoCompactCarriedPrefixEstimatedTokens = EstimateContextTokens(autoCompactionUsage.CarriedPrefixWeight),
                AutoCompactBodyAfterPrefixEstimatedTokens = EstimateContextTokens(autoCompactionUsage.BodyAfterPrefixWeight),
                AutoCompactInstructionsCharacters = agentDefaults.AutoCompactInstructions.Length,
                ConfiguredCompactPromptCharacters = projectInstructionOptions.CompactPrompt.Length,
                ConfiguredCompactPromptSourceLabel = projectInstructionOptions.CompactPromptSourceLabel,
                HasConfiguredCompactPromptOverride = projectInstructionOptions.HasCompactPromptOverride,
                CompactedSourceMessages = compaction?.SourceMessageCount ?? 0,
                CompactionSummaryCharacters = compaction?.Summary.Length ?? 0,
                CompactionRequests = conversation?.CompactionUsage?.RequestCount ?? 0,
                CompactionUsage = conversation?.CompactionUsage?.Usage ?? CopilotTokenUsage.Empty,
                ConversationGoalCharacters = conversation?.Goal?.Objective.Length ?? 0,
                ConversationGoalState = conversation?.Goal?.State,
                ConversationGoalTimeUsedSeconds = conversation?.Goal?.TimeUsedSeconds ?? 0,
                ConversationGoalContinuationDeferred = conversation?.IsGoalContinuationDeferred == true,
                ConversationGoalActive = conversation?.Goal?.IsActive == true,
                ConversationGoalAchieved = conversation?.Goal?.IsAchieved == true,
                AttachmentCount = Attachments.Count,
                FileAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.File),
                ImageAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.Image),
                WebAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.WebPage),
                HasLiveWindowContext = HasCurrentLiveContext,
                AgentContextEnabled = agentContextEnabled,
                ProjectInstructionDocuments = projectInstructions.Length,
                ProjectInstructionPromptCharacters = CopilotAgentProjectInstructions.BuildPromptBlock(projectInstructions).Length,
                ProjectInstructionMaximumBytes = projectInstructionOptions.MaximumBytes,
                ProjectInstructionUsesCodexConfig = projectInstructionOptions.UsesCodexConfig,
                ProjectInstructionConfigSourceLabel = projectInstructionOptions.ConfigSourceLabel,
                ProjectInstructionProjectTrustLabel = projectInstructionOptions.ProjectTrustLabel,
                ProjectInstructionDeveloperInstructionsCharacters = projectInstructionOptions.DeveloperInstructions.Length,
                ProjectInstructionDeveloperInstructionsSourceLabel = projectInstructionOptions.DeveloperInstructionsSourceLabel,
                ProjectInstructionHasDeveloperInstructionsOverride = projectInstructionOptions.HasDeveloperInstructionsOverride,
                ProjectInstructionFallbackFileNames = projectInstructionOptions.FallbackFileNames,
                ProjectInstructionRootMarkers = projectInstructionOptions.ProjectRootMarkers,
                ProjectInstructionHasRootMarkersOverride = projectInstructionOptions.HasProjectRootMarkersOverride,
                ProjectInstructionAppliedProjectConfigFilePaths = projectInstructionOptions.AppliedProjectConfigFilePaths,
                TrustedProjectRootPaths = trustedProjectRoots,
                ProjectInstructions = projectInstructions,
                RecordedSkillRuns = skillUsage?.RecordedRuns ?? 0,
                TrackedSkills = skillUsage?.Entries.Count ?? 0,
                HistoricalExplicitOnlySkills = skillUsage?.HistoricalExplicitOnlySkills.Count ?? 0,
                ManualSkillOverrides = agentDefaults.SkillOverrides.Count,
                SkillMetadataCharacterBudget = CopilotAgentSkills.ResolveMetadataCharacterBudget(
                    agentDefaults.ContextWindowTokens),
                AgentContextWindowTokens = agentDefaults.ContextWindowTokens,
                AgentRequestTokenBudget = agentDefaults.RequestTokenBudget,
                AgentMaxToolCalls = agentDefaults.MaxToolCalls,
                AgentMaxPasses = agentDefaults.MaxAgentPasses,
                AgentTimeoutSeconds = agentDefaults.TimeoutSeconds,
                RegisteredCapabilities = capabilitySnapshot.Capabilities.Count,
                EnabledExternalMcpServers = _config.ExternalMcpServers.Count(server => server?.Enabled == true),
                ToolHookSurface = toolHookSurface,
                AgentExtensions = agentExtensionSnapshot.Sources,
                AgentExtensionIssues = agentExtensionSnapshot.Issues,
            });
        }

        private void HandleProjectInstructionCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotProjectInstructionDiagnostics.ParseCommand(arguments);
            var snapshot = CaptureProjectInstructionSnapshot();
            if (request.Action == CopilotProjectInstructionCommandAction.List)
            {
                ShowLocalCommandResult(
                    command,
                    CopilotProjectInstructionDiagnostics.Format(
                        snapshot,
                        ActiveHostedRun?.IsAgent == true));
                return;
            }
            if (request.Action == CopilotProjectInstructionCommandAction.Invalid)
            {
                ShowLocalCommandResult(command, CopilotProjectInstructionDiagnostics.Usage);
                return;
            }

            var document = CopilotProjectInstructionDiagnostics.FindByPosition(
                snapshot.Documents,
                request.Position);
            if (document == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前生效项目指令中没有 #{request.Position:N0}。输入 /memory 查看实时顺序；目标文件或规则可能已变化。");
                return;
            }

            var errorMessage = string.Empty;
            var additionalAllowedRoots = new[] { snapshot.GlobalInstructionRootPath };
            if (!CopilotLocalFileLinkNavigator.TryResolve(document.Path, additionalAllowedRoots, out var target)
                || !CopilotLocalFileLinkNavigator.TryOpen(target, additionalAllowedRoots, out errorMessage))
            {
                ShowLocalCommandResult(
                    command,
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? "该指令文件已不存在、不在当前工作区内，或当前没有可用编辑器。"
                        : CopilotUserFacingErrorFormatter.Sanitize(errorMessage));
                return;
            }

            ShowLocalCommandResult(
                command,
                $"已在内置编辑器中打开 #{request.Position:N0} · {Path.GetFileName(document.Path)}。"
                + Environment.NewLine
                + (ActiveHostedRun?.IsAgent == true
                    ? "当前运行中的任务仍使用请求启动时捕获的指令快照；保存后的内容从后续请求开始生效。"
                    : "保存后的内容会在下一次需要工作区证据的 Agent 请求启动时重新发现并加载。"));
        }

        private CopilotProjectInstructionSnapshot CaptureProjectInstructionSnapshot()
        {
            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var trustedProjectRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(turnSnapshot);
            var documents = CopilotAgentProjectInstructions.DiscoverWithGlobal(
                trustedProjectRoots,
                turnSnapshot.ActiveDocumentPath,
                turnSnapshot.Attachments
                    .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                    .Select(attachment => attachment.Value),
                turnSnapshot.GlobalInstructionRootPath,
                turnSnapshot.ProjectInstructionDiscoveryOptions);
            return new CopilotProjectInstructionSnapshot(
                trustedProjectRoots.Count > 0
                    ? trustedProjectRoots[0]
                    : turnSnapshot.SolutionDirectoryPath,
                turnSnapshot.ActiveDocumentPath,
                turnSnapshot.GlobalInstructionRootPath,
                turnSnapshot.ProjectInstructionDiscoveryOptions,
                documents);
        }

        private static int EstimateContextTokens(long weight) => weight <= 0
            ? 0
            : CopilotTokenEstimator.WeightToTokenEstimate(weight);

        private void DismissLocalCommandResult()
        {
            LocalCommandResultTitle = string.Empty;
            LocalCommandResultText = string.Empty;
        }

        private void RunUiOperation(Func<Task> operation, string operationName, Action<string>? onError = null)
        {
            CopilotUiTaskObserver.Run(
                operation,
                operationName,
                onError ?? (message =>
                {
                    LocalCommandResultTitle = operationName + " · 失败";
                    LocalCommandResultText = message;
                }));
        }
    }
}
