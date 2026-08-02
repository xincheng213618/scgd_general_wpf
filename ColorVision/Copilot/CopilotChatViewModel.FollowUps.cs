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
        private void ExecutePrimaryAction()
        {
            if (IsPromptHistorySearchOpen)
            {
                TryCompletePromptHistorySearch();
                return;
            }
            if (_isCompactingConversation)
            {
                _compactConversationCts?.RequestCancellation();
                return;
            }
            if (_fileAttachmentCts != null)
            {
                _fileAttachmentCts.RequestCancellation();
                return;
            }
            if (_webPageAttachmentCts != null)
            {
                _webPageAttachmentCts.RequestCancellation();
                return;
            }
            if (IsViewingQueuedRun || IsViewingActiveRun)
            {
                if (IsViewingActiveRun && ActiveHostedRunInteraction.PrimaryAction == CopilotHostedRunPrimaryAction.None)
                    return;
                StopCurrentReply();
                return;
            }

            RunUiOperation(SendAsync, "发送请求");
        }

        private void ExecuteSendOrSteer()
        {
            if (IsPromptHistorySearchOpen)
            {
                TryCompletePromptHistorySearch();
                return;
            }
            if (IsViewingActiveRun)
            {
                if (IsAnsweringUserQuestion)
                {
                    TryAnswerCurrentUserQuestion(InputText);
                    return;
                }

                if (TryHandleComposerLocalCommandDuringRun(InputText, out var recognized)
                    || recognized)
                {
                    return;
                }

                if (DefaultFollowUpBehavior == CopilotFollowUpBehavior.Queue)
                    TryQueueCurrentRunFollowUp();
                else
                    TrySteerCurrentRun();
                return;
            }
            if (IsViewingQueuedRun)
                return;

            RunUiOperation(SendAsync, "发送请求");
        }

        private bool CanAnswerUserQuestionOption(CopilotUserQuestionOption? option)
        {
            var question = ActiveUserQuestion;
            return option != null
                && IsAnsweringUserQuestion
                && question != null
                && string.Equals(option.RequestId, question.RequestId, StringComparison.Ordinal)
                && string.Equals(option.TaskId, question.TaskId, StringComparison.Ordinal)
                && question.Options.Any(candidate =>
                    string.Equals(candidate.Label, option.Label, StringComparison.Ordinal));
        }

        private void AnswerUserQuestionOption(CopilotUserQuestionOption? option)
        {
            if (CanAnswerUserQuestionOption(option))
                TryAnswerCurrentUserQuestion(option!.Label);
        }

        private bool TryAnswerCurrentUserQuestion(string? answer)
        {
            var run = ActiveHostedRun;
            var message = ActiveUserQuestionMessage;
            var question = message?.UserQuestion;
            if (run == null
                || message == null
                || question?.IsPending != true
                || !IsAnsweringUserQuestion
                || !CopilotUserQuestionSnapshot.TryNormalizeAnswer(answer, out var normalized)
                || !_turnRuntime.TryAnswerUserQuestion(run.Id, question.RequestId, normalized))
            {
                return false;
            }

            message.UserQuestion = question.Resolve(CopilotUserQuestionResolution.Answered, normalized);
            InputText = string.Empty;
            NotifyUserQuestionStateChanged();
            return true;
        }

        private bool TrySteerCurrentRun()
        {
            var steeringMessage = (InputText ?? string.Empty).Trim();
            var activeRun = ActiveHostedRun;
            if (!CanSteerCurrentRun || activeRun == null || string.IsNullOrWhiteSpace(steeringMessage))
                return false;
            if (TryHandleComposerLocalCommandDuringRun(steeringMessage, out var recognizedLocalCommand))
                return true;
            if (recognizedLocalCommand)
                return false;
            if (SelectedProfile == null
                || !TryValidateComposerCharacterLimit(steeringMessage)
                || !TryValidatePromptBudget(steeringMessage, activeRun.Mode, SelectedProfile))
            {
                return false;
            }
            var admission = _turnRuntime.EnqueueSteeringMessage(
                activeRun.Id,
                steeringMessage);
            if (!admission.IsAccepted)
            {
                ReportSteeringAdmissionFailure(admission);
                return false;
            }

            var activeConversation = Conversations.FirstOrDefault(conversation => string.Equals(conversation.Id, activeRun.ConversationId, StringComparison.Ordinal));
            var steeringSnapshot = new CopilotSteeringMessageSnapshot(
                admission.MessageId,
                steeringMessage);
            if (activeConversation == null
                || !CopilotSteeringRecovery.TrackPending(
                    activeConversation,
                    activeRun.Id,
                    steeringSnapshot,
                    DateTimeOffset.UtcNow))
            {
                LocalCommandResultTitle = "运行中指令已发送，输入已保留";
                LocalCommandResultText = "运行时已接受这条指令，但无法建立可靠的恢复记录；输入未清空，请确认 Agent 响应后再处理。";
                PersistState(immediate: true);
                return true;
            }
            var activeAssistant = activeConversation?.Messages.LastOrDefault(message => !message.IsUser && message.IsThinkingInProgress);
            if (activeAssistant != null)
                CopilotAssistantMessagePresenter.AppendExecutionTrace(activeAssistant, "User steering queued · " + CopilotAgentTraceEntry.Sanitize(steeringMessage));

            DismissLocalCommandResult();
            InputText = string.Empty;
            PersistState(immediate: true);
            return true;
        }

        internal static string GetSteeringAdmissionFailureText(
            CopilotSteeringAdmissionResult admission) => admission.Reason switch
        {
            CopilotSteeringAdmissionReason.InvalidInput =>
                "这条运行中指令为空、过长，或缺少有效的任务标识。输入已保留，请修改后重试。",
            CopilotSteeringAdmissionReason.PendingUserQuestion =>
                "Agent 正在等待问题回答。输入已保留，请先回答问题，再发送运行中指令。",
            CopilotSteeringAdmissionReason.NoActiveTask =>
                "当前 Agent 已结束或已切换任务。输入已保留，请直接发送，或重新排到下一轮。",
            CopilotSteeringAdmissionReason.QueueFull =>
                "运行中指令缓冲区已满。输入已保留，请等待 Agent 消费已有指令后重试，或改为排到下一轮。",
            CopilotSteeringAdmissionReason.RuntimeUnavailable =>
                "运行中指令未能送达。输入已保留，请重试，或改为排到下一轮。",
            _ => "运行中指令未发送。输入已保留，请重试。",
        };

        private void ReportSteeringAdmissionFailure(
            CopilotSteeringAdmissionResult admission)
        {
            LocalCommandResultTitle = "运行中指令未发送";
            LocalCommandResultText = GetSteeringAdmissionFailureText(admission);
        }

    }
}
