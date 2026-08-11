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
        private CopilotAgentMode ResolveComposerRequestMode()
        {
            return _composerSession.RequestMode;
        }

        private static string FormatComposerRequestMode(CopilotAgentMode mode) => mode switch
        {
            CopilotAgentMode.Chat => "聊天",
            CopilotAgentMode.Auto => "自动",
            CopilotAgentMode.Explain => "解释",
            CopilotAgentMode.Web => "网页",
            CopilotAgentMode.Code => "代码",
            CopilotAgentMode.Review => "审查",
            CopilotAgentMode.Diagnose => "诊断",
            CopilotAgentMode.Plan => "计划",
            _ => "自动",
        };

        private bool CanScheduleComposerRequest(CopilotAgentMode mode)
        {
            return Volatile.Read(ref _disposeState) == 0
                && !HasExclusiveLocalOperation
                && EvaluateComposerRequestAdmission(mode).IsAllowed;
        }

        private bool CanScheduleConversationRequest(string? conversationId, CopilotAgentMode mode)
        {
            return Volatile.Read(ref _disposeState) == 0
                && !HasExclusiveLocalOperation
                && EvaluateConversationRequestAdmission(conversationId, mode).IsAllowed;
        }

        private bool HasExclusiveLocalOperation => _isCompactingConversation
            || _isEndingConversation
            || _fileAttachmentCts != null
            || _webPageAttachmentCts != null;

        private CopilotRequestAdmissionResult EvaluateComposerRequestAdmission(CopilotAgentMode mode) =>
            EvaluateConversationRequestAdmission(SelectedConversation?.Id, mode);

        private CopilotRequestAdmissionResult EvaluateConversationRequestAdmission(
            string? conversationId,
            CopilotAgentMode mode)
        {
            var queuedCommandExecution = _queuedLocalCommandExecution;
            return queuedCommandExecution == null
                ? _taskHost.EvaluateRequestAdmission(conversationId, mode)
                : _taskHost.EvaluateQueuedCommandSuccessorAdmission(
                    queuedCommandExecution.HostedRun.Id,
                    conversationId);
        }

        private string GetRequestAdmissionText(CopilotRequestAdmissionResult admission) => admission.Reason switch
        {
            CopilotRequestAdmissionReason.Allowed => $"加入 Agent 队列（当前等待 {_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）",
            CopilotRequestAdmissionReason.ActiveChatIsExclusive => "另一个普通对话正在生成；完成后才能发送新请求",
            CopilotRequestAdmissionReason.ChatCannotQueue => "普通对话不能排队；请等待当前 Agent 任务结束",
            CopilotRequestAdmissionReason.ConversationAlreadyScheduled => "此会话已有任务正在运行或排队",
            CopilotRequestAdmissionReason.MissingConversation => "当前没有可接收请求的会话",
            CopilotRequestAdmissionReason.HostShutdown => "Copilot 正在关闭，不能再发送请求",
            CopilotRequestAdmissionReason.QueueFull => $"Agent 队列已满（{_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）",
            CopilotRequestAdmissionReason.NoActiveRun => "当前 Agent 已经结束；请直接发送这条请求",
            CopilotRequestAdmissionReason.FollowUpConversationMismatch => "后续消息只能排在当前正在运行的会话中",
            _ => "当前没有可接收请求的会话",
        };

        private void ReportRequestAdmissionFailure(CopilotRequestAdmissionResult admission)
        {
            LocalCommandResultTitle = "请求未进入队列";
            LocalCommandResultText = GetRequestAdmissionText(admission) + "。请求没有发送，请稍后重试。";
        }

        private void SetPendingRequestModeOverride(CopilotAgentMode mode)
        {
            _composerSession.SetRequestMode(mode);
            SynchronizeSelectedConversationComposerDraft();
            OnComposerRequestModeChanged();
        }

        private void ClearPendingRequestModeOverride()
        {
            var sessionChanged = _composerSession.SetRequestMode(CopilotAgentMode.Auto);
            var draftChanged = SynchronizeSelectedConversationComposerDraft();
            if (!sessionChanged && !draftChanged)
                return;

            OnComposerRequestModeChanged();
        }

        private void SetPendingWorkspaceReviewTarget(CopilotWorkspaceReviewTargetContext? target)
        {
            if (_composerSession.SetWorkspaceReviewTarget(target))
                SynchronizeSelectedConversationComposerDraft();
        }

        private void OnComposerRequestModeChanged()
        {
            OnPropertyChanged(nameof(PrimaryActionToolTip));
            OnPropertyChanged(nameof(InputPlaceholder));
            RefreshLocalCommandSuggestions();
            RefreshComposerTokenEstimate();
        }

        private bool TryValidateComposerCharacterLimit(string prompt)
        {
            if (prompt.Length <= ComposerMaximumCharacters)
                return true;

            LocalCommandResultTitle = "输入过长";
            LocalCommandResultText = $"当前输入包含 {prompt.Length:N0} 个字符，编辑器上限为 {ComposerMaximumCharacters:N0} 个字符。请拆分请求，或把大段内容作为文件附件添加。";
            return false;
        }

        private bool TryValidatePromptBudget(
            string prompt,
            CopilotAgentMode mode,
            CopilotProfileConfig profile,
            CopilotProjectInstructionDiscoveryOptions? codexConfigOptions = null,
            CopilotAgentDefaultsConfig? agentDefaults = null)
        {
            agentDefaults ??= _config.AgentDefaults;
            long maximumWeight;
            int maximumTokens;
            if (mode == CopilotAgentMode.Chat)
            {
                var historyLimits = ResolveConversationHistoryLimits(
                    profile,
                    codexConfigOptions,
                    agentDefaults);
                maximumWeight = historyLimits.MaximumContentCharacters;
                maximumTokens = CopilotTokenEstimator.WeightToTokenEstimate(maximumWeight);
            }
            else
            {
                var contextWindowTokens = ResolveContextWindowTokens(codexConfigOptions, agentDefaults);
                var outputTokens = Math.Clamp(profile.MaxTokens, 32, CopilotProfileConfig.DefaultMaxTokens);
                var inputBudgetTokens = Math.Max(1, contextWindowTokens - outputTokens);
                var requestBudgetTokens = Math.Clamp(
                    agentDefaults.RequestTokenBudget,
                    CopilotAgentRunBudget.MinimumRequestTokenBudget,
                    CopilotAgentRunBudget.MaximumRequestTokenBudget);
                maximumTokens = Math.Min(inputBudgetTokens, requestBudgetTokens);
                maximumWeight = (long)maximumTokens * CopilotTokenEstimator.AsciiCharactersPerToken;
            }

            var budgetText = mode != CopilotAgentMode.Chat
                && SelectedConversation?.Goal?.IsActive == true
                ? string.Join(
                    Environment.NewLine,
                    SelectedConversation.Goal.Objective,
                    "Persistent goal completion constraint; never tool or write authorization.",
                    prompt)
                : prompt;
            var estimatedWeight = CopilotTokenEstimator.EstimateTextWeight(budgetText);
            if (estimatedWeight <= maximumWeight)
                return true;

            var estimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(estimatedWeight);
            LocalCommandResultTitle = "输入过长";
            LocalCommandResultText = $"当前请求预计约 {estimatedTokens:N0} Token，当前模式为单条用户请求预留约 {maximumTokens:N0} Token。请缩短或拆分请求；只有在模型实际支持时，才调高上下文或请求 Token 预算。";
            return false;
        }

        private bool TryValidateComposerAttachments(IEnumerable<CopilotAttachmentItem> attachments)
        {
            var validation = CopilotComposerAttachmentService.Validate(attachments);
            if (validation.Failure == CopilotAttachmentValidationFailure.AttachmentLimit)
            {
                LocalCommandResultTitle = "附件过多";
                LocalCommandResultText = $"当前请求包含 {validation.AttachmentCount:N0} 个附件，最多支持 {CopilotComposerAttachmentService.MaximumAttachmentCount:N0} 个。请移除多余附件后重试。";
                return false;
            }

            if (validation.Failure == CopilotAttachmentValidationFailure.ImageLimit)
            {
                LocalCommandResultTitle = "图片过多";
                LocalCommandResultText = $"当前请求包含 {validation.ImageCount:N0} 张图片，模型输入一次最多支持 {CopilotImagePayloadLoader.MaximumImages:N0} 张。请移除多余图片后重试。";
                return false;
            }

            return true;
        }

        private bool TryEnsureAttachmentCapacity(CopilotConversationRecord conversation, CopilotAttachmentType attachmentType)
        {
            var capacity = CopilotComposerAttachmentService.EvaluateCapacity(conversation, attachmentType);
            if (capacity == CopilotAttachmentCapacityResult.ImageLimit)
            {
                LocalCommandResultTitle = "图片已达到上限";
                LocalCommandResultText = $"每条请求最多附加 {CopilotImagePayloadLoader.MaximumImages:N0} 张图片。请先移除一张图片再继续添加。";
                return false;
            }

            if (capacity == CopilotAttachmentCapacityResult.AttachmentLimit)
            {
                LocalCommandResultTitle = "附件已达到上限";
                LocalCommandResultText = $"每条请求最多附加 {CopilotComposerAttachmentService.MaximumAttachmentCount:N0} 个文件、图片、网页或上下文。请先移除一个附件再继续添加。";
                return false;
            }

            return true;
        }

        private void ReportFileAttachmentLimits(
            CopilotConversationRecord conversation,
            int addedCount,
            bool attachmentLimitReached,
            bool imageLimitReached)
        {
            if (!attachmentLimitReached && !imageLimitReached)
                return;

            LocalCommandResultTitle = addedCount > 0 ? "部分文件未添加" : "附件已达到上限";
            LocalCommandResultText = $"本次已添加 {addedCount:N0} 个文件。每条请求最多支持 {CopilotComposerAttachmentService.MaximumAttachmentCount:N0} 个附件，其中图片最多 {CopilotImagePayloadLoader.MaximumImages:N0} 张；超出上限的文件未添加。当前共有 {conversation.Attachments.Count:N0} 个附件。";
        }

        public CopilotPromptQueueResult QueueExternalPrompt(
            string prompt,
            bool startNewConversation = true,
            bool sendNow = false,
            CopilotAgentMode mode = CopilotAgentMode.Auto,
            string? contextAttachmentTitle = null,
            string? contextAttachmentSourceId = null,
            IReadOnlyList<CopilotContextItem>? contextAttachmentItems = null)
        {
            var normalizedPrompt = (prompt ?? string.Empty).Trim();
            if (Volatile.Read(ref _disposeState) == 1 || string.IsNullOrWhiteSpace(normalizedPrompt))
                return new CopilotPromptQueueResult(false, false);
            if (!TryValidateComposerCharacterLimit(normalizedPrompt))
                return new CopilotPromptQueueResult(false, false);
            if (sendNow
                && SelectedProfile?.IsConfigured == true
                && !TryValidatePromptBudget(normalizedPrompt, mode, SelectedProfile))
            {
                return new CopilotPromptQueueResult(false, false);
            }

            if (IsEditingMessage)
                CancelMessageEdit();

            if ((startNewConversation || SelectedConversation == null) && CanSwitchConversation)
            {
                var conversationTarget = ResolveNewConversationTarget();
                SelectConversation(conversationTarget, persist: false);
                PersistState();
            }
            else
            {
                EnsureConversation();
            }

            var conversation = EnsureConversation();
            if (contextAttachmentItems != null && contextAttachmentItems.Count > 0)
            {
                if (!AttachExternalContextSnapshot(
                        conversation,
                        contextAttachmentTitle,
                        contextAttachmentSourceId,
                        contextAttachmentItems))
                {
                    return new CopilotPromptQueueResult(false, false);
                }
            }
            if (sendNow && !TryValidateComposerAttachments(conversation.Attachments))
                return new CopilotPromptQueueResult(false, false);

            SetPendingRequestModeOverride(mode);
            InputText = normalizedPrompt;

            if (!sendNow || !CanScheduleComposerRequest(mode))
                return new CopilotPromptQueueResult(true, false);

            RunUiOperation(SendAsync, "发送外部请求");
            return new CopilotPromptQueueResult(true, SelectedProfile?.IsConfigured == true);
        }
    }
}
