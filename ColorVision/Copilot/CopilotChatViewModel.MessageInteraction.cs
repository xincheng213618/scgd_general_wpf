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
        private void CopyMessage(CopilotChatMessage? message)
        {
            if (message == null)
                return;

            var text = BuildMessageClipboardText(message);
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!TrySetClipboardText(text, out var errorMessage))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to copy message: {errorMessage}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static bool TrySetClipboardText(string text, out string errorMessage)
        {
            try
            {
                Clipboard.SetText(text);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
                return false;
            }
        }

        private bool CanEditMessage(CopilotChatMessage? message)
        {
            return !IsBusy
                && message?.IsUser == true
                && TryResolveLatestTurn(message, out _, out _, out _);
        }

        private bool CanBranchConversation(CopilotChatMessage? message)
        {
            return CanSwitchConversation
                && !IsEditingMessage
                && message?.IsUser == false
                && !message.IsThinkingInProgress
                && !string.IsNullOrWhiteSpace(message.Content)
                && SelectedConversation?.Messages.Contains(message) == true;
        }

        private bool CanOpenBranchOrigin(CopilotConversationRecord? branch)
        {
            var origin = branch == null
                ? null
                : CopilotConversationBranchService.FindBranchOriginTarget(Conversations, branch);
            return CanSwitchConversation
                && branch != null
                && origin != null
                && !origin.IsArchived;
        }

        private void OpenBranchOrigin(CopilotConversationRecord? branch)
        {
            if (!CanOpenBranchOrigin(branch))
                return;

            var origin = CopilotConversationBranchService.FindBranchOriginTarget(Conversations, branch!);
            if (origin != null)
                SelectConversation(origin, persist: true, preferredProfileId: origin.ProfileId);
        }

        private void ForkCurrentConversation(CopilotLocalCommand command, string requestedTitle)
        {
            var source = SelectedConversation;
            var normalizedTitle = (requestedTitle ?? string.Empty).Trim();
            if (source == null || IsEditingMessage || !CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前状态不能创建会话分支；请先结束消息编辑或等待当前普通对话完成。");
                return;
            }
            if (normalizedTitle.Length > CopilotConversationRecord.MaximumTitleCharacters)
            {
                ShowLocalCommandResult(
                    command,
                    $"会话分支名称不能超过 {CopilotConversationRecord.MaximumTitleCharacters:N0} 个字符。");
                return;
            }

            var branchPoint = CopilotConversationBranchService.FindCurrentBranchPoint(source);
            if (branchPoint == null)
            {
                ShowLocalCommandResult(command, "当前会话还没有可分叉的回答。请先开始至少一轮对话。");
                return;
            }

            try
            {
                var capturedRunningTurn = branchPoint.IsThinkingInProgress;
                var branch = CreateAndSelectCurrentConversationBranch(source, normalizedTitle);
                ShowLocalCommandResult(
                    command,
                    $"已从“{source.Title}”复制 {branch.Messages.Count:N0} 条消息到“{branch.Title}”。"
                    + Environment.NewLine
                    + (capturedRunningTurn
                        ? "源会话中的 Agent 仍会继续运行；分支已将当前回答标记为运行中快照，未完成工具不会在分支中继续。"
                        : "原会话保持不变；这里只分叉聊天历史，不会创建 Git 分支或回滚当前工作区。")
                    + Environment.NewLine
                    + "未发送草稿、编辑区附件、Agent checkpoint 与会话级授权不会继承。");
            }
            catch (Exception ex)
            {
                ShowLocalCommandResult(
                    command,
                    "无法创建会话分支：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
        }

        private void RewindConversation(CopilotLocalCommand command, string requestedOrdinal)
        {
            var source = SelectedConversation;
            if (source == null || IsBusy || IsEditingMessage || !CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前状态不能回溯会话；请先结束正在运行的请求或消息编辑。");
                return;
            }

            if (string.IsNullOrWhiteSpace(requestedOrdinal))
            {
                ShowLocalCommandResult(command, CopilotConversationRewindService.Format(source));
                return;
            }
            if (!CopilotConversationRewindService.TryResolve(source, requestedOrdinal, out var point))
            {
                ShowLocalCommandResult(command, "回溯序号必须对应一条现有用户请求，例如 /rewind 1。输入 /rewind 可查看可用回溯点。");
                return;
            }

            if (source.Attachments.Count > 0)
            {
                var replaceAttachments = MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "回溯会用所选历史请求的附件快照替换当前待发送附件；源会话和文件不会改变。是否继续？",
                    "ColorVision",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (replaceAttachments != MessageBoxResult.Yes)
                {
                    ShowLocalCommandResult(command, "会话回溯已取消；当前会话和待发送附件均未改变。");
                    return;
                }
            }

            try
            {
                var restoredAttachments = point.UserMessage.AttachmentSnapshotCaptured
                    ? point.UserMessage.Attachments.Select(attachment => attachment.CreateSnapshot()).ToArray()
                    : Array.Empty<CopilotAttachmentItem>();
                if (restoredAttachments.Length > CopilotComposerAttachmentService.MaximumAttachmentCount)
                    throw new InvalidOperationException($"历史请求包含超过 {CopilotComposerAttachmentService.MaximumAttachmentCount:N0} 个附件，不能安全恢复到输入框。");

                var branch = CopilotConversationBranchService.CreateRewindBranch(
                    source,
                    point.UserMessage);
                foreach (var attachment in restoredAttachments)
                    branch.Attachments.Add(attachment);
                branch.DraftText = point.UserMessage.Content;
                InsertAndSelectConversationBranch(branch);

                _pendingAgentRecoveryRequest = null;
                ClearPendingRequestModeOverride();
                SetPendingRequestModeOverride(Enum.IsDefined(point.UserMessage.RequestMode)
                    ? point.UserMessage.RequestMode
                    : CopilotAgentMode.Chat);
                InputText = point.UserMessage.Content;
                UpdateAttachmentsState(branch);

                var attachmentText = point.AttachmentCount > 0
                    ? $"，并恢复 {point.AttachmentCount:N0} 个附件快照"
                    : point.UserMessage.HasAttachments
                        ? "；该旧请求没有可靠的附件快照，附件未恢复"
                        : string.Empty;
                ShowLocalCommandResult(
                    command,
                    $"已从“{source.Title}”创建回溯分支“{branch.Title}”，定位到倒数第 {point.Ordinal:N0} 条请求之前。"
                    + Environment.NewLine
                    + $"原请求已恢复到输入框{attachmentText}，可修改后发送；不会自动执行。"
                    + Environment.NewLine
                    + "源会话、当前文件和外部操作保持不变；Agent checkpoint 与临时授权未继承。");
            }
            catch (Exception ex)
            {
                ShowLocalCommandResult(
                    command,
                    "无法回溯会话：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
        }

        private void BranchConversation(CopilotChatMessage? message)
        {
            if (!CanBranchConversation(message) || SelectedConversation == null)
                return;

            try
            {
                CreateAndSelectConversationBranch(SelectedConversation, message!, requestedTitle: null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"无法创建会话分支：{CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                MessageBoxImage.Warning);
            }
        }

        private CopilotConversationRecord CreateAndSelectConversationBranch(
            CopilotConversationRecord source,
            CopilotChatMessage throughAssistantMessage,
            string? requestedTitle)
        {
            var branch = CopilotConversationBranchService.CreateBranch(source, throughAssistantMessage, requestedTitle);
            return InsertAndSelectConversationBranch(branch);
        }

        private CopilotConversationRecord CreateAndSelectCurrentConversationBranch(
            CopilotConversationRecord source,
            string? requestedTitle)
        {
            var branch = CopilotConversationBranchService.CreateCurrentBranch(source, requestedTitle);
            return InsertAndSelectConversationBranch(branch);
        }

        private CopilotConversationRecord InsertAndSelectConversationBranch(CopilotConversationRecord branch)
        {
            CopilotConversationService.Insert(Conversations, branch);
            SelectConversation(branch, persist: false, preferredProfileId: branch.ProfileId);
            PersistState(immediate: true);
            return branch;
        }

        private void BeginEditMessage(CopilotChatMessage? message)
        {
            if (!CanEditMessage(message)
                || !TryResolveLatestTurn(message, out var conversation, out var userMessage, out _))
            {
                return;
            }

            if (string.Equals(_editingConversationId, conversation.Id, StringComparison.Ordinal)
                && string.Equals(_editingUserMessageId, userMessage.Id, StringComparison.Ordinal))
            {
                return;
            }

            if (!IsInputEmpty || conversation.Attachments.Count > 0)
            {
                var replaceDraft = MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "编辑上一条请求会暂时替换当前草稿和待发送附件；取消编辑时会恢复。是否继续？",
                    "ColorVision",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (replaceDraft != MessageBoxResult.Yes)
                    return;
            }

            _composerDraftBeforeMessageEdit = new CopilotComposerDraftSnapshot(
                conversation.Id,
                InputText,
                ResolveComposerRequestMode(),
                conversation.Attachments.Select(attachment => attachment.CreateSnapshot()).ToArray());
            var messageAttachments = (userMessage.AttachmentSnapshotCaptured
                    ? userMessage.Attachments
                    : conversation.Attachments)
                .Select(attachment => attachment.CreateSnapshot())
                .ToArray();
            conversation.Attachments.Clear();
            foreach (var attachment in messageAttachments)
                conversation.Attachments.Add(attachment);

            _pendingAgentRecoveryRequest = null;
            DismissLocalCommandResult();
            SetMessageEditState(conversation.Id, userMessage.Id);
            SetPendingRequestModeOverride(userMessage.RequestMode);
            InputText = userMessage.Content;
            UpdateAttachmentsState(conversation);
        }

        private void CancelMessageEdit()
        {
            if (!IsEditingMessage)
                return;

            var conversation = Conversations.FirstOrDefault(candidate => string.Equals(candidate.Id, _editingConversationId, StringComparison.Ordinal));
            var draftSnapshot = _composerDraftBeforeMessageEdit;
            _composerDraftBeforeMessageEdit = null;
            SetMessageEditState(string.Empty, string.Empty);
            _pendingAgentRecoveryRequest = null;

            if (conversation == null || !ReferenceEquals(conversation, SelectedConversation))
            {
                ClearPendingRequestModeOverride();
                InputText = string.Empty;
                return;
            }

            conversation.Attachments.Clear();
            if (draftSnapshot != null && string.Equals(draftSnapshot.ConversationId, conversation.Id, StringComparison.Ordinal))
            {
                foreach (var attachment in draftSnapshot.Attachments)
                    conversation.Attachments.Add(attachment.CreateSnapshot());
                SetPendingRequestModeOverride(draftSnapshot.RequestMode);
                InputText = draftSnapshot.Text;
            }
            else
            {
                ClearPendingRequestModeOverride();
                InputText = string.Empty;
            }
            UpdateAttachmentsState(conversation);
        }

        private void SetMessageEditState(string conversationId, string userMessageId)
        {
            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            var normalizedUserMessageId = (userMessageId ?? string.Empty).Trim();
            if (string.Equals(_editingConversationId, normalizedConversationId, StringComparison.Ordinal)
                && string.Equals(_editingUserMessageId, normalizedUserMessageId, StringComparison.Ordinal))
            {
                return;
            }

            _editingConversationId = normalizedConversationId;
            _editingUserMessageId = normalizedUserMessageId;
            OnPropertyChanged(nameof(IsEditingMessage));
            OnPropertyChanged(nameof(InputPlaceholder));
            RefreshLocalCommandSuggestions();
            NotifyPromptHistoryPrefixCompletionChanged();
            OnPropertyChanged(nameof(HasLocalCommandResult));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanRegenerateMessage(CopilotChatMessage? message)
        {
            if (IsBusy || IsEditingMessage || message == null || SelectedConversation == null || SelectedProfile == null || !SelectedProfile.IsConfigured)
                return false;

            return TryResolveLatestTurn(message, out var conversation, out _, out var assistantMessage)
                && !CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                    conversation,
                    assistantMessage,
                    CreateConversationRequestProfile(SelectedProfile, conversation),
                    CopilotCapabilityCatalog.Shared.GetSnapshot());
        }

        private async Task RetryMessageAsync(CopilotChatMessage? message, bool refreshExternalContext)
        {
            if (!TryResolveLatestTurn(message, out var conversation, out var userMessage, out var assistantMessage))
                return;

            if (SelectedProfile == null || !SelectedProfile.IsConfigured)
            {
                OpenSettings();
                return;
            }
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                assistantMessage,
                CreateConversationRequestProfile(SelectedProfile, conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot()))
            {
                return;
            }

            var prompt = (userMessage.Content ?? string.Empty).Trim();
            var modelPrompt = CopilotPlanHandoff.ResolveEffectiveUserText(prompt, userMessage.RequestContent);
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            var requestProfile = CreateConversationRequestProfile(SelectedProfile, conversation);
            if (!TryValidateComposerCharacterLimit(modelPrompt)
                || !TryValidatePromptBudget(modelPrompt, userMessage.RequestMode, requestProfile))
            {
                return;
            }

            var turnSnapshot = CaptureHostedTurnSnapshot(conversation, userMessage);
            if (!TryValidateComposerAttachments(turnSnapshot.Attachments))
                return;

            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;
            conversation.AgentSessionCheckpoint = null;
            PersistState();

            var hostedRun = _taskHost.Start(
                conversation.Id,
                userMessage.RequestMode,
                run => ExecuteHostedRetryAsync(run, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot, refreshExternalContext));
            await AwaitHostedRunCompletionAsync(hostedRun);
        }

        private async Task ExecuteHostedRetryAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage? assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshExternalContext)
        {
            CopilotChatMessage? replacementAssistantMessage = null;
            try
            {
                if (assistantMessage != null)
                    conversation.Messages.Remove(assistantMessage);

                replacementAssistantMessage = CreatePendingAssistantMessage(requestProfile, userMessage.RequestMode);
                conversation.Messages.Add(replacementAssistantMessage);
            }
            catch (Exception ex)
            {
                if (replacementAssistantMessage == null)
                {
                    replacementAssistantMessage = CreatePendingAssistantMessage(requestProfile, userMessage.RequestMode);
                    conversation.Messages.Add(replacementAssistantMessage);
                }

                CopilotHostedTurnCompletion.CompleteFailure(conversation, replacementAssistantMessage, ex.Message, requestProfile.ApiKey);
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
                RefreshAgentTasks();
                return;
            }

            await ExecuteHostedPreparedTurnAsync(
                hostedRun,
                conversation,
                requestProfile,
                userMessage,
                replacementAssistantMessage,
                turnSnapshot,
                refreshExternalContext);
        }

        private bool TryResolveLatestTurn(CopilotChatMessage? message, out CopilotConversationRecord conversation, out CopilotChatMessage userMessage, out CopilotChatMessage? assistantMessage)
        {
            conversation = SelectedConversation!;
            userMessage = null!;
            assistantMessage = null;

            if (message == null || SelectedConversation == null)
                return false;

            var messages = SelectedConversation.Messages;
            var targetIndex = messages.IndexOf(message);
            if (targetIndex < 0)
                return false;

            var userIndex = message.IsUser ? targetIndex : FindPreviousUserMessageIndex(messages, targetIndex - 1);
            if (userIndex < 0)
                return false;

            var resolvedAssistantIndex = userIndex + 1 < messages.Count && !messages[userIndex + 1].IsUser
                ? userIndex + 1
                : -1;

            if (!message.IsUser && resolvedAssistantIndex != targetIndex)
                return false;

            var turnEndIndex = resolvedAssistantIndex >= 0 ? resolvedAssistantIndex : userIndex;
            if (turnEndIndex != messages.Count - 1)
                return false;

            conversation = SelectedConversation;
            userMessage = messages[userIndex];
            assistantMessage = resolvedAssistantIndex >= 0 ? messages[resolvedAssistantIndex] : null;
            return true;
        }

        private bool TryResolvePendingMessageEdit(
            CopilotConversationRecord conversation,
            out int userIndex,
            out CopilotChatMessage userMessage,
            out CopilotChatMessage? assistantMessage)
        {
            userIndex = -1;
            userMessage = null!;
            assistantMessage = null;
            if (!IsEditingMessage
                || !string.Equals(_editingConversationId, conversation.Id, StringComparison.Ordinal))
            {
                return false;
            }

            var candidate = conversation.Messages.FirstOrDefault(message =>
                message.IsUser && string.Equals(message.Id, _editingUserMessageId, StringComparison.Ordinal));
            if (candidate == null
                || !TryResolveLatestTurn(candidate, out var resolvedConversation, out userMessage, out assistantMessage)
                || !ReferenceEquals(resolvedConversation, conversation))
            {
                userMessage = null!;
                assistantMessage = null;
                return false;
            }

            userIndex = conversation.Messages.IndexOf(userMessage);
            return userIndex >= 0;
        }

        private static int FindPreviousUserMessageIndex(ObservableCollection<CopilotChatMessage> messages, int startIndex)
        {
            for (var index = startIndex; index >= 0; index--)
            {
                if (messages[index].IsUser)
                    return index;
            }

            return -1;
        }

        private static string BuildMessageClipboardText(CopilotChatMessage message)
        {
            return (message.Content ?? string.Empty).Trim();
        }


    }
}
