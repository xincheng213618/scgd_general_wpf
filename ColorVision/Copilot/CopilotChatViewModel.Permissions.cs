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
        private void ApprovalCoordinator_PendingActionsInvalidated(object? sender, EventArgs e)
        {
            if (Volatile.Read(ref _disposeState) != 0)
                return;
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ApprovalCoordinator_PendingActionsInvalidated(sender, e)));
                return;
            }

            RefreshPendingActions();
        }

        private void RefreshMcpStatus()
        {
            _hasPendingMcpActions = _approvalCoordinator.TotalPendingCount > 0;
            _hasRecentMcpFailures = CopilotMcpAuditLogger.GetRecentEntries(20)
                .Any(entry => !entry.Success && DateTimeOffset.UtcNow - entry.TimestampUtc <= RecentMcpFailureWindow);

            OnPropertyChanged(nameof(IsMcpEnabled));
            OnPropertyChanged(nameof(IsMcpRunning));
            OnPropertyChanged(nameof(IsControlModeVisible));
            OnPropertyChanged(nameof(HasPendingMcpActions));
            OnPropertyChanged(nameof(HasRecentMcpFailures));
            OnPropertyChanged(nameof(McpStatusLabel));
            OnPropertyChanged(nameof(McpStatusToolTip));
            OnPropertyChanged(nameof(PrimaryActionToolTip));
        }

        private void RefreshPendingActions()
        {
            _approvalCoordinator.RefreshPendingActions(SelectedConversation?.Id);

            OnPropertyChanged(nameof(HasPendingActions));
            OnPropertyChanged(nameof(HasPendingActionPanel));
            OnPropertyChanged(nameof(PendingActionPanelTitle));
            OnPropertyChanged(nameof(PendingActionPanelSummary));
            OnPropertyChanged(nameof(PendingActionPanelToolTip));
            RefreshMcpStatus();
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshTimedAccessAndPendingActions()
        {
            var conversation = SelectedConversation;
            if (conversation?.ExpireFullAccessGrantIfNeeded() == true)
            {
                OnComposerAccessModeChanged();
                SetPendingActionFeedback("临时自动复核授权已到期，受保护操作恢复按需确认。");
            }
            else if (conversation?.AccessMode == CopilotAgentAccessMode.FullAccess)
            {
                var currentWorkspacePath = CaptureHostedTurnSnapshot(conversation.Attachments).SolutionDirectoryPath;
                if (!AccessWorkspacePathsMatch(conversation.FullAccessWorkspacePath, currentWorkspacePath)
                    && conversation.RevokeFullAccessGrant())
                {
                    OnComposerAccessModeChanged();
                    SetPendingActionFeedback("工作区已变化，临时自动复核授权已撤销。");
                }
            }
            RefreshProviderRateLimitStatus();
            RefreshPendingActions();
        }

        private void CopyPendingActionId(ConfirmableAction? action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.ActionId))
                return;

            try
            {
                Clipboard.SetText(action.ActionId);
                SetPendingActionFeedback($"Copied action_id {action.ActionId}.");
            }
            catch (Exception ex)
            {
                SetPendingActionFeedback($"Copy failed: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}");
            }
        }

        private void CopyPendingActionPayload(ConfirmableAction? action)
        {
            if (action == null)
                return;

            try
            {
                Clipboard.SetText(action.ConfirmActionPayloadJson);
                SetPendingActionFeedback($"Copied confirm_action payload for {action.ActionId}.");
            }
            catch (Exception ex)
            {
                SetPendingActionFeedback($"Copy failed: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}");
            }
        }

        private async Task ApprovePendingActionAsync(ConfirmableAction? action)
        {
            if (!CanReviewPendingAction(action))
            {
                SetPendingActionFeedback("当前会话、任务或工作区与这条审批请求不匹配，已拒绝代为批准。");
                RefreshPendingActions();
                return;
            }

            var reviewWindow = new CopilotActionReviewWindow(action!);
            var owner = Application.Current.GetActiveWindow();
            if (owner != null)
                reviewWindow.Owner = owner;
            if (reviewWindow.ShowDialog() != true)
            {
                SetPendingActionFeedback($"未批准操作 {action!.ActionId}。");
                return;
            }
            if (!CanReviewPendingAction(action))
            {
                SetPendingActionFeedback($"操作 {action!.ActionId} 已失效、被取消，或不再属于当前任务；没有执行。");
                RefreshPendingActions();
                return;
            }

            var approvalScope = CaptureApprovalScope();
            if (action!.ExecuteOnApproval)
            {
                var cancellation = BeginAuxiliaryOperation();
                try
                {
                    var approvalResult = await _approvalCoordinator.ApproveAsync(
                        action,
                        approvalScope,
                        cancellation.Token);
                    SetPendingActionFeedback(approvalResult.Message);
                }
                finally
                {
                    CompleteAuxiliaryOperation(cancellation);
                }
            }
            else
            {
                var approvalResult = await _approvalCoordinator.ApproveAsync(
                    action,
                    approvalScope,
                    CancellationToken.None);
                SetPendingActionFeedback(approvalResult.Message);
            }
            RefreshPendingActions();
        }

        private void SetComposerAccessMode(CopilotAgentAccessMode mode)
        {
            var conversation = SelectedConversation;
            if (conversation == null || !Enum.IsDefined(mode))
                return;

            if (mode == CopilotAgentAccessMode.ConfirmProtectedActions)
            {
                if (!conversation.RevokeFullAccessGrant())
                    return;

                OnComposerAccessModeChanged();
                SetPendingActionFeedback("已恢复按需确认。已有待审批操作保持原状态。");
                PersistState(immediate: true);
                return;
            }

            if (conversation.AccessMode == CopilotAgentAccessMode.FullAccess)
                return;

            var turnSnapshot = CaptureHostedTurnSnapshot(conversation.Attachments);
            if (string.IsNullOrWhiteSpace(turnSnapshot.SolutionDirectoryPath))
            {
                SetPendingActionFeedback("请先打开一个项目工作区，再启用临时自动复核。");
                return;
            }

            var activeRun = ActiveHostedRun;
            var taskId = activeRun?.IsAgent == true
                && string.Equals(activeRun.ConversationId, conversation.Id, StringComparison.Ordinal)
                ? activeRun?.Id ?? string.Empty
                : string.Empty;
            conversation.PrepareFullAccessGrant(
                turnSnapshot.SolutionDirectoryPath,
                taskId,
                DateTimeOffset.UtcNow.Add(CopilotAgentAccessContext.MaximumFullAccessLifetime));
            OnComposerAccessModeChanged();
            SetPendingActionFeedback(string.IsNullOrWhiteSpace(taskId)
                ? "已为下一任务启用临时自动复核（最长 15 分钟）。工作区补丁及回滚仍按确定性范围规则批准；其他受保护调用由独立模型复核，风险较高、无法判断或复核失败时仍等待用户。已有待审批操作不受影响。"
                : "已为本任务启用临时自动复核（最长 15 分钟）。工作区补丁及回滚仍按确定性范围规则批准；其他受保护调用由独立模型复核，风险较高、无法判断或复核失败时仍等待用户。已有待审批操作不受影响。");
            PersistState(immediate: true);
        }

        private async Task RetryAutomaticallyDeniedActionAsync(
            CopilotAutomaticApprovalDenialSnapshot denial)
        {
            var conversation = SelectedConversation;
            var workspacePath = CaptureHostedTurnSnapshot(
                conversation?.Attachments ?? Enumerable.Empty<CopilotAttachmentItem>()).SolutionDirectoryPath;
            if (conversation == null
                || !string.Equals(conversation.Id, denial.ConversationId, StringComparison.Ordinal)
                || !AccessWorkspacePathsMatch(denial.WorkspacePath, workspacePath))
            {
                SetPendingActionFeedback("会话或工作区已变化；一次性重试票据已保留，但没有自动发起重试。");
                return;
            }

            var retryInstruction = BuildAutomaticApprovalRetryInstruction(denial.ToolName);
            var activeRun = ActiveHostedRun;
            if (activeRun != null)
            {
                if (!string.Equals(activeRun.ConversationId, conversation.Id, StringComparison.Ordinal)
                    || !activeRun.IsAgent)
                {
                    SetPendingActionFeedback("另一个任务仍在运行；一次性重试票据已保留，请回到本会话后继续。");
                    return;
                }

                var admission = _turnRuntime.EnqueueSteeringMessage(activeRun.Id, retryInstruction);
                if (!admission.IsAccepted)
                {
                    SetPendingActionFeedback(
                        "一次性重试票据已保留，但当前 Agent 未接受重试指令："
                        + GetSteeringAdmissionFailureText(admission));
                    return;
                }

                var steeringSnapshot = new CopilotSteeringMessageSnapshot(
                    admission.MessageId,
                    retryInstruction);
                var recoveryTracked = CopilotSteeringRecovery.TrackPending(
                    conversation,
                    activeRun.Id,
                    steeringSnapshot,
                    DateTimeOffset.UtcNow);
                var activeAssistant = conversation.Messages.LastOrDefault(message =>
                    !message.IsUser && message.IsThinkingInProgress);
                if (activeAssistant != null)
                {
                    CopilotAssistantMessagePresenter.AppendExecutionTrace(
                        activeAssistant,
                        "User authorized one exact retry of an automatic-review denial for "
                        + CopilotAgentTraceEntry.Sanitize(denial.ToolName)
                        + "; the retry still requires automatic review.");
                }

                PersistState(immediate: true);
                SetPendingActionFeedback(recoveryTracked
                    ? "已向当前 Agent 发送精确重试指令；仅原工具及原参数可消费一次票据，重试仍会经过自动审查。"
                    : "当前 Agent 已接受精确重试指令，但恢复记录未能保存；一次性票据仍只匹配原动作。请观察本轮结果。");
                return;
            }

            if (!CanScheduleComposerRequest(CopilotAgentMode.Auto))
            {
                SetPendingActionFeedback("当前状态不能立即开始重试；一次性重试票据已保留。");
                return;
            }

            var latestAssistant = conversation.Messages.LastOrDefault(message => !message.IsUser);
            if (latestAssistant != null
                && conversation.AgentSessionCheckpoint?.IsStructurallyValid() == true)
            {
                _pendingAgentRecoveryRequest = new CopilotAgentRecoveryRequest
                {
                    Mode = CopilotAgentRecoveryMode.RetryDeniedAction,
                    PreviousStopReason = latestAssistant.AgentStopReason,
                    ToolName = denial.ToolName,
                };
            }

            SetPendingRequestModeOverride(CopilotAgentMode.Auto);
            InputText = retryInstruction;
            await SendAsync();
        }

        private static string BuildAutomaticApprovalRetryInstruction(string? toolName)
        {
            var normalizedToolName = CopilotAgentTraceEntry.Sanitize(toolName);
            if (string.IsNullOrWhiteSpace(normalizedToolName))
                normalizedToolName = "刚才被拒绝的受保护工具";
            return "用户已通过 /approve 为 "
                + normalizedToolName
                + " 的那一次精确拒绝动作授权一次重试。仅在原任务仍需要时，使用完全相同的参数发起一次新的调用；不要改写、扩大或用相似动作代替。该调用仍必须经过自动审查，若再次拒绝就停止并说明原因。";
        }

        private bool CanReviewPendingAction(ConfirmableAction? action)
        {
            return action != null
                && _approvalCoordinator.Evaluate(action, CaptureApprovalScope()).CanReview;
        }

        private CopilotApprovalScope CaptureApprovalScope()
        {
            var conversation = SelectedConversation;
            var activeRun = ActiveHostedRun;
            var workspacePath = CaptureHostedTurnSnapshot(
                conversation?.Attachments ?? Enumerable.Empty<CopilotAttachmentItem>()).SolutionDirectoryPath;
            return new CopilotApprovalScope(
                conversation?.Id ?? string.Empty,
                activeRun?.IsAgent == true ? activeRun.ConversationId : string.Empty,
                activeRun?.IsAgent == true ? activeRun.Id : string.Empty,
                workspacePath);
        }

        private string BuildFullAccessToolTip()
        {
            var conversation = SelectedConversation;
            var scope = conversation?.IsFullAccessPreparedForNextTask == true ? "下一任务" : "本任务";
            var workspace = string.IsNullOrWhiteSpace(conversation?.FullAccessWorkspacePath)
                ? "当前 ColorVision 应用"
                : conversation.FullAccessWorkspacePath;
            var expires = conversation?.FullAccessExpiresAtUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "15 分钟内";
            return $"临时自动复核仅对{scope}及工作区“{workspace}”有效，最晚 {expires} 失效。已预览的工作区补丁及回滚仍按逐文件路径和 SHA-256 的确定性规则批准；其他受保护调用仅在提供完整原生审批详情时，才由独立、无工具的权限模型复核，每次复核会增加一次模型调用。仅 LOW/MEDIUM 风险可自动批准，HIGH/CRITICAL、详情缺失或过长、格式错误、超时或模型失败仍等待用户。任务结束、工作区变化或应用重启后恢复按需确认。";
        }

        private static bool WorkspacePathsMatch(string expectedPath, string currentPath)
        {
            if (string.IsNullOrWhiteSpace(expectedPath))
                return true;
            if (string.IsNullOrWhiteSpace(currentPath))
                return false;

            try
            {
                return string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedPath)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentPath)),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool AccessWorkspacePathsMatch(string grantedPath, string currentPath)
        {
            if (string.IsNullOrWhiteSpace(grantedPath) || string.IsNullOrWhiteSpace(currentPath))
            {
                return string.IsNullOrWhiteSpace(grantedPath)
                    && string.IsNullOrWhiteSpace(currentPath);
            }

            return WorkspacePathsMatch(grantedPath, currentPath);
        }

        private void OnComposerAccessModeChanged()
        {
            OnPropertyChanged(nameof(ComposerAccessMode));
            OnPropertyChanged(nameof(IsComposerFullAccess));
            OnPropertyChanged(nameof(IsComposerConfirmAccess));
            OnPropertyChanged(nameof(ComposerAccessModeLabel));
            OnPropertyChanged(nameof(ComposerAccessModeToolTip));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ApprovalCoordinator_ActionTransitioned(
            object? sender,
            CopilotApprovalActionTransitionEventArgs e)
        {
            if (Volatile.Read(ref _disposeState) != 0)
                return;
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ApprovalCoordinator_ActionTransitioned(sender, e)));
                return;
            }

            var result = _approvalCoordinator.ApplyTransition(e.Transition);
            if (result.StateChanged)
                PersistState();
        }

        private void RejectPendingAction(ConfirmableAction? action)
        {
            if (!CanReviewPendingAction(action))
            {
                SetPendingActionFeedback("当前会话、任务或工作区与这条审批请求不匹配，未执行拒绝操作。");
                RefreshPendingActions();
                return;
            }

            var result = _approvalCoordinator.Reject(action, CaptureApprovalScope());
            SetPendingActionFeedback($"{action!.ActionId}: {result.Message}");
            RefreshPendingActions();
        }

        private void SetPendingActionFeedback(string message)
        {
            _pendingActionFeedbackCts?.RequestCancellation();
            var cts = new CopilotNonBlockingCancellationSource();
            _pendingActionFeedbackCts = cts;
            PendingActionFeedbackText = message ?? string.Empty;
            _ = ClearPendingActionFeedbackAsync(cts);
        }

        private async Task ClearPendingActionFeedbackAsync(CopilotNonBlockingCancellationSource cts)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
                if (!ReferenceEquals(_pendingActionFeedbackCts, cts))
                    return;

                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => ClearPendingActionFeedback(cts)));
                    return;
                }

                ClearPendingActionFeedback(cts);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                cts.Dispose();
            }
        }

        private void ClearPendingActionFeedback(CopilotNonBlockingCancellationSource cts)
        {
            if (!ReferenceEquals(_pendingActionFeedbackCts, cts))
                return;

            _pendingActionFeedbackCts = null;
            PendingActionFeedbackText = string.Empty;
        }

    }
}
