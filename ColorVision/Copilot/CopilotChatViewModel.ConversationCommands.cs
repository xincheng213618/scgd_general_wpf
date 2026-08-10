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
        private void StartNewChat(
            CopilotCodexSessionStartSource? sessionStartSource = null)
        {
            if (!CanSwitchConversation)
                return;
            if (IsEditingMessage)
                CancelMessageEdit();
            _pendingAgentRecoveryRequest = null;
            ClearPendingRequestModeOverride();

            if (CopilotConversationService.IsReusableEmpty(SelectedConversation))
            {
                if (sessionStartSource.HasValue && SelectedConversation != null)
                    _turnRuntime.QueueSessionStart(SelectedConversation.Id, sessionStartSource.Value);
                return;
            }

            var conversation = ResolveNewConversationTarget();
            if (!ReferenceEquals(conversation, SelectedConversation))
            {
                SelectConversation(conversation, persist: false);
                PersistState();
            }
            if (sessionStartSource.HasValue)
                _turnRuntime.QueueSessionStart(conversation.Id, sessionStartSource.Value);
        }

        private void ClearConversationContext(CopilotLocalCommand command, string previousTitle)
        {
            if (IsBusy || !CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再清空上下文。");
                return;
            }

            var normalizedTitle = previousTitle.Trim();
            if (normalizedTitle.Length > 0
                && (SelectedConversation == null
                    || !TryApplyConversationTitle(SelectedConversation, normalizedTitle)))
            {
                ShowLocalCommandResult(
                    command,
                    $"旧会话名称不能为空且不能超过 {CopilotConversationRecord.MaximumTitleCharacters:N0} 个字符。");
                return;
            }

            DismissLocalCommandResult();
            StartNewChat(CopilotCodexSessionStartSource.Clear);
        }

        private void ResumeConversation(CopilotLocalCommand command, string query)
        {
            if (!CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前状态不能切换会话；请先结束消息编辑或等待当前普通对话完成。");
                return;
            }

            var normalizedQuery = NormalizeConversationSearchText(query.Trim());
            var exactMatch = CopilotConversationService.FindUniqueResumeTarget(
                CopilotConversationArchiveService.GetActive(Conversations),
                normalizedQuery);
            if (exactMatch != null)
            {
                ConversationSearchText = string.Empty;
                DismissLocalCommandResult();
                SelectConversation(exactMatch, persist: true, preferredProfileId: exactMatch.ProfileId);
                return;
            }

            ConversationSearchText = normalizedQuery;
            RefreshFilteredConversations();
            DismissLocalCommandResult();
            ConversationSearchRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task ArchiveCurrentConversationAsync(CopilotLocalCommand command)
        {
            var conversation = SelectedConversation;
            if (conversation == null || conversation.IsArchived)
            {
                ShowLocalCommandResult(command, "当前没有可归档的活动会话。");
                return;
            }
            if (IsBusy || !CanSwitchConversation || HasExclusiveLocalOperation)
            {
                ShowLocalCommandResult(command, "当前会话仍有请求或本地操作正在执行，请完成或停止后再归档。");
                return;
            }
            var activeBackgroundCommands =
                CopilotBackgroundShellCommandRegistry.Shared.GetSnapshots(
                        conversation.Id)
                    .Count(snapshot => snapshot.IsActive);
            if (activeBackgroundCommands > 0)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话还有 {activeBackgroundCommands:N0} 条后台命令在运行；请先使用 /ps 查看并停止，再归档会话。进程树未改变。");
                return;
            }
            var retentionBlocker = GetConversationRetentionBlocker(conversation);
            if (retentionBlocker != CopilotConversationRetentionBlocker.None)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话{CopilotConversationRetentionPolicy.Describe(retentionBlocker)}；请先处理该状态，避免把待办隐藏。");
                return;
            }

            _isEndingConversation = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var hookDiagnostics = await EndConversationSessionAsync(conversation);
                if (!Conversations.Contains(conversation) || conversation.IsArchived)
                    return;

                var archivedTitle = conversation.Title;
                AcknowledgeCompletionNotices(conversation.Id);
                conversation.ReplaceAgentActivity(null);
                conversation.IsArchived = true;
                conversation.Touch();
                conversation.RefreshSummary();
                var activeConversations = CopilotConversationArchiveService.GetActive(Conversations);
                var replacement = activeConversations.Count > 0
                    ? activeConversations[0]
                    : CreateConversation();
                SelectConversation(replacement, persist: false, preferredProfileId: replacement.ProfileId);
                RefreshCompactHistoryConversations();
                RefreshFilteredConversations();
                RefreshConversationBranchFamily();
                PersistState(immediate: true);
                ShowLocalCommandResult(
                    command,
                    $"已归档“{archivedTitle}”。内容仍保留，但已从常用会话列表和 /resume 中隐藏。\n\n"
                    + "使用 /archived 查看，或 /unarchive <会话 ID 或唯一完整标题> 恢复。"
                    + FormatSessionEndHookDiagnostics(hookDiagnostics));
            }
            finally
            {
                _isEndingConversation = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void UnarchiveConversation(CopilotLocalCommand command, string query)
        {
            if (IsBusy || !CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前状态不能恢复归档会话；请先结束消息编辑或等待当前请求完成。");
                return;
            }

            var normalizedQuery = (query ?? string.Empty).Trim();
            var conversation = CopilotConversationArchiveService.FindUniqueArchived(
                Conversations,
                normalizedQuery);
            if (conversation == null)
            {
                ShowLocalCommandResult(
                    command,
                    CopilotConversationArchiveService.FormatArchived(Conversations, normalizedQuery));
                return;
            }

            conversation.IsArchived = false;
            conversation.Touch();
            conversation.RefreshSummary();
            _turnRuntime.QueueSessionStart(
                conversation.Id,
                CopilotCodexSessionStartSource.Resume);
            CopilotConversationService.MoveToPreferredIndex(Conversations, conversation);
            RefreshCompactHistoryConversations();
            RefreshFilteredConversations();
            SelectConversation(conversation, persist: false, preferredProfileId: conversation.ProfileId);
            PersistState(immediate: true);
            ShowLocalCommandResult(command, $"已恢复“{conversation.Title}”，会话内容和草稿均保持不变。");
        }

        private CopilotConversationRetentionBlocker GetConversationRetentionBlocker(
            CopilotConversationRecord conversation)
        {
            var conversationId = conversation.Id;
            return CopilotConversationRetentionPolicy.Evaluate(
                conversation,
                hasScheduledRun: _taskHost.FindRunByConversationId(conversationId) != null,
                hasPendingApproval: _approvalCoordinator.HasPendingActionsForConversation(conversationId),
                hasQueuedFollowUp: QueuedFollowUps.Any(item => string.Equals(
                    item.ConversationId,
                    conversationId,
                    StringComparison.Ordinal)),
                isEditingMessage: string.Equals(
                    _editingConversationId,
                    conversationId,
                    StringComparison.Ordinal));
        }

        private void RenameCurrentConversation(CopilotLocalCommand command, string requestedTitle)
        {
            var conversation = SelectedConversation;
            if (!CanRenameConversation(conversation))
            {
                ShowLocalCommandResult(command, "当前没有可重命名的会话。");
                return;
            }

            if (string.IsNullOrWhiteSpace(requestedTitle))
            {
                DismissLocalCommandResult();
                RenameConversation(conversation);
                return;
            }

            if (!TryApplyConversationTitle(conversation!, requestedTitle))
            {
                ShowLocalCommandResult(
                    command,
                    $"会话名称不能为空且不能超过 {CopilotConversationRecord.MaximumTitleCharacters:N0} 个字符。");
                return;
            }

            DismissLocalCommandResult();
        }

        private void CopyAssistantResponse(CopilotLocalCommand command, string requestedOrdinal)
        {
            if (!CopilotConversationService.TryParseAssistantResponseOrdinal(requestedOrdinal, out var ordinal))
            {
                ShowLocalCommandResult(command, "序号必须是大于 0 的整数，例如 /copy 或 /copy 2。");
                return;
            }

            var message = CopilotConversationService.FindNthLatestCompletedAssistantResponse(
                SelectedConversation,
                ordinal);
            if (message == null)
            {
                ShowLocalCommandResult(
                    command,
                    ordinal == 1
                        ? "当前会话还没有可复制的已完成回答。"
                        : $"当前会话没有倒数第 {ordinal:N0} 条可复制的已完成回答。");
                return;
            }

            var text = BuildMessageClipboardText(message);
            if (!TrySetClipboardText(text, out var errorMessage))
            {
                ShowLocalCommandResult(command, "复制失败：" + errorMessage);
                return;
            }

            ShowLocalCommandResult(
                command,
                ordinal == 1
                    ? $"已复制最近一条已完成回答（{text.Length:N0} 个字符）。"
                    : $"已复制倒数第 {ordinal:N0} 条已完成回答（{text.Length:N0} 个字符）。");
        }

        private void RetryLatestResponse(
            CopilotLocalCommand command,
            string arguments)
        {
            if (!CopilotResponseRetryCommand.TryParse(
                    arguments,
                    out var refreshExternalContext))
            {
                ShowLocalCommandResult(
                    command,
                    "参数只支持 refresh，例如 /retry 或 /retry refresh。");
                return;
            }

            var message = SelectedConversation?.Messages.LastOrDefault();
            if (message == null)
            {
                ShowLocalCommandResult(command, "当前会话还没有可重试的请求。");
                return;
            }
            if (SelectedProfile?.IsConfigured != true)
            {
                ShowLocalCommandResult(
                    command,
                    "当前模型 Profile 尚未完成配置；请先使用 /settings models。");
                return;
            }
            if (!CanRegenerateMessage(message))
            {
                if (TryResolveLatestTurn(
                        message,
                        out var conversation,
                        out _,
                        out var assistantMessage)
                    && assistantMessage != null
                    && CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                        conversation,
                        assistantMessage,
                        CreateCurrentConversationRequestProfile(SelectedProfile, conversation),
                        CopilotCapabilityCatalog.Shared.GetSnapshot(
                            _currentCodexConfigOptions.ConfiguredPluginsEnabled),
                        CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                            _currentCodexConfigOptions.ConfiguredHooksEnabled,
                            _currentCodexConfigOptions.ConfiguredPluginsEnabled,
                            _currentCodexConfigOptions.ConfiguredCommandHooks)))
                {
                    ShowLocalCommandResult(
                        command,
                        "最后一轮保留了可安全继续的 Agent checkpoint；请优先使用 /tasks 继续或明确放弃恢复项，避免重新执行已完成的工具操作。");
                    return;
                }

                ShowLocalCommandResult(
                    command,
                    "最后一轮当前不能重试；请先结束运行或消息编辑，并确认它仍是当前会话的最后一轮。");
                return;
            }

            DismissLocalCommandResult();
            RunUiOperation(
                () => RetryMessageAsync(message, refreshExternalContext),
                refreshExternalContext
                    ? "刷新附件与网页后重新生成"
                    : message.RequestMode == CopilotAgentMode.Chat
                        ? "重新生成回复"
                        : "重新运行 Agent");
        }

        private void SelectModelProfile(CopilotLocalCommand command, string query)
        {
            if (!CanSelectProfile)
            {
                ShowLocalCommandResult(
                    command,
                    IsBusy
                        ? "当前有请求正在执行，请完成或停止后再切换模型 Profile。"
                        : "当前没有可选择的模型 Profile，请先在 Copilot 设置中添加并配置模型。");
                return;
            }

            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length == 0)
            {
                DismissLocalCommandResult();
                ProfileSelectionRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            var profile = CopilotConversationService.FindUniqueProfileTarget(Profiles, normalizedQuery);
            if (profile == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"未找到唯一匹配“{normalizedQuery}”的 Profile 名或模型 ID，请从模型列表中选择。");
                ProfileSelectionRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            SelectedProfile = profile;
            ShowLocalCommandResult(
                command,
                $"当前会话后续请求将使用：{profile.DisplayLabel}"
                + Environment.NewLine
                + $"模型：{(string.IsNullOrWhiteSpace(profile.Model) ? "未设置" : profile.Model)}"
                + Environment.NewLine
                + $"协议：{profile.ProviderLabel}"
                + Environment.NewLine
                + $"推理：{profile.ReasoningLabel}"
                + Environment.NewLine
                + $"状态：{profile.ConfigurationStatusText}");
        }

        private void SelectReasoningMode(CopilotLocalCommand command, string query)
        {
            if (!CanSelectProfile)
            {
                ShowLocalCommandResult(
                    command,
                    IsBusy
                        ? "当前有请求正在执行，请完成或停止后再调整推理强度。"
                        : "当前没有可选择的模型 Profile，请先在 Copilot 设置中添加并配置模型。");
                return;
            }

            var profile = SelectedProfile;
            if (profile == null)
            {
                ShowLocalCommandResult(command, "当前没有选中的模型 Profile。");
                return;
            }
            if (!HasConfigurableReasoning)
            {
                ShowLocalCommandResult(
                    command,
                    $"{profile.DisplayLabel} 未声明可配置的推理强度，将继续使用 Provider 默认值。");
                return;
            }

            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length == 0)
            {
                DismissLocalCommandResult();
                ReasoningSelectionRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            var option = CopilotReasoningCapabilities.FindCommandOption(profile, normalizedQuery);
            if (option == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前 Profile 不支持推理级别“{normalizedQuery}”。"
                    + Environment.NewLine
                    + $"可用级别：{CopilotReasoningCapabilities.GetCommandOptionSummary(profile)}");
                return;
            }

            var previousMode = CopilotReasoningCapabilities.GetEffectiveMode(profile);
            SetSelectedProfileReasoningMode(option.Mode);
            var changeLabel = previousMode == option.Mode ? "保持" : "已设置";
            ShowLocalCommandResult(
                command,
                $"{profile.DisplayLabel} · 推理强度{changeLabel}为“{option.Label}”。"
                + Environment.NewLine
                + option.Description
                + Environment.NewLine
                + "该设置保存到当前模型 Profile，并用于后续请求。");
        }

        private void SelectResponsePersonality(CopilotLocalCommand command, string query)
        {
            var conversation = EnsureConversation();
            var codexConfigOptions = CaptureHostedTurnSnapshot(
                Array.Empty<CopilotAttachmentItem>()).ProjectInstructionDiscoveryOptions;
            var previousResolution = CopilotResponsePersonalitySelection.Resolve(
                conversation,
                codexConfigOptions);
            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length == 0)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话风格：{CopilotResponsePersonalitySelection.GetDisplayName(previousResolution.Personality)}"
                    + $"（{CopilotResponsePersonalitySelection.GetCommandToken(previousResolution.Personality)}）"
                    + $" · 来源 {previousResolution.SourceLabel}"
                    + Environment.NewLine
                    + "可用风格：friendly、pragmatic、none。");
                return;
            }
            if (!CopilotResponsePersonalitySelection.TryParse(normalizedQuery, out var personality))
            {
                ShowLocalCommandResult(
                    command,
                    $"不支持会话风格“{normalizedQuery}”。"
                    + Environment.NewLine
                    + "可用风格：friendly、pragmatic、none。");
                return;
            }

            var alreadyExplicit = conversation.HasResponsePersonalityOverride
                || conversation.ResponsePersonality != CopilotResponsePersonality.None;
            conversation.ResponsePersonality = personality;
            conversation.HasResponsePersonalityOverride = true;
            conversation.Touch();
            PersistState(immediate: true);
            var nextResolution = CopilotResponsePersonalitySelection.Resolve(
                conversation,
                codexConfigOptions);
            var changeLabel = alreadyExplicit && previousResolution.Personality == personality
                ? "保持"
                : "已设置";
            var checkpointNote = conversation.AgentSessionCheckpoint == null
                || previousResolution.Personality == nextResolution.Personality
                ? string.Empty
                : Environment.NewLine + "已有 Agent checkpoint 会保留；继续任务时将按新风格重新规划，不会直接复用旧提示身份。";
            var featureNote = codexConfigOptions.ConfiguredPersonalityEnabled
                ? "它只影响后续回答的默认表达，不改变任务范围、权限、安全规则、证据要求或用户明确指定的格式。"
                : "该选择已保存，但当前 features.personality=false，后续请求不会注入 personality 指令；重新启用该功能后生效。";
            ShowLocalCommandResult(
                command,
                $"当前会话风格{changeLabel}为“{CopilotResponsePersonalitySelection.GetDisplayName(personality)}”（{CopilotResponsePersonalitySelection.GetCommandToken(personality)}）。"
                + Environment.NewLine
                + featureNote
                + checkpointNote);
        }

        private CopilotConversationRecord ResolveNewConversationTarget()
        {
            var profile = SelectedProfile ?? ResolveProfile(_state.ActiveProfileId) ?? _config.GetPreferredDefaultProfile();
            return CopilotConversationService.ResolveNewTarget(Conversations, SelectedConversation, profile);
        }
    }
}
