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
        private void ConfirmationStore_ActionsChanged(object? sender, EventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ConfirmationStore_ActionsChanged(sender, e)));
                return;
            }

            RefreshPendingActions();
        }

        private void RefreshMcpStatus()
        {
            _hasPendingMcpActions = CopilotMcpConfirmationStore.Instance.PendingCount > 0;
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
            _pendingActions.Clear();
            foreach (var action in CopilotMcpConfirmationStore.Instance.GetPendingActionsForConversation(
                SelectedConversation?.Id))
            {
                _pendingActions.Add(action);
            }

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

            var reviewContext = CreateConfirmationReviewContext();
            if (action!.ExecuteOnApproval)
            {
                var cancellation = BeginAuxiliaryOperation();
                try
                {
                    var approvalResult = await CopilotMcpConfirmationDecision.ApproveAsync(
                        CopilotMcpConfirmationStore.Instance,
                        action,
                        reviewContext,
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
                var approvalResult = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    reviewContext,
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

        private bool CanReviewPendingAction(ConfirmableAction? action)
        {
            if (action?.Status != ConfirmableActionStatus.Pending
                || !action.CanReviewFromConversation(SelectedConversation?.Id))
            {
                return false;
            }

            var requestContext = action.RequestContext;
            if (requestContext.SourceKind == CopilotApprovalSourceKind.InAppAgent)
            {
                var activeRun = ActiveHostedRun;
                if (activeRun == null
                    || !string.Equals(activeRun.ConversationId, requestContext.ConversationId, StringComparison.Ordinal)
                    || !string.Equals(activeRun.Id, requestContext.TaskId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            var currentWorkspacePath = CaptureHostedTurnSnapshot(
                SelectedConversation?.Attachments ?? Enumerable.Empty<CopilotAttachmentItem>()).SolutionDirectoryPath;
            return requestContext.SourceKind is CopilotApprovalSourceKind.InAppAgent or CopilotApprovalSourceKind.ExternalMcp
                ? AccessWorkspacePathsMatch(requestContext.WorkspacePath, currentWorkspacePath)
                : WorkspacePathsMatch(requestContext.WorkspacePath, currentWorkspacePath);
        }

        private CopilotConfirmationReviewContext CreateConfirmationReviewContext()
        {
            var conversation = SelectedConversation;
            var activeRun = ActiveHostedRun;
            var taskId = activeRun?.IsAgent == true
                && string.Equals(activeRun.ConversationId, conversation?.Id, StringComparison.Ordinal)
                ? activeRun?.Id ?? string.Empty
                : string.Empty;
            var workspacePath = CaptureHostedTurnSnapshot(
                conversation?.Attachments ?? Enumerable.Empty<CopilotAttachmentItem>()).SolutionDirectoryPath;
            return new CopilotConfirmationReviewContext(
                conversation?.Id ?? string.Empty,
                taskId,
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

        private void ConfirmationStore_ActionStatusChanged(object? sender, ConfirmableActionChangedEventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ConfirmationStore_ActionStatusChanged(sender, e)));
                return;
            }

            var action = e.Action;
            if (string.IsNullOrWhiteSpace(action.AgentCallId))
                return;

            var owningConversations = action.RequestContext.SourceKind == CopilotApprovalSourceKind.InAppAgent
                && !string.IsNullOrWhiteSpace(action.RequestContext.ConversationId)
                ? Conversations.Where(conversation => string.Equals(
                    conversation.Id,
                    action.RequestContext.ConversationId,
                    StringComparison.Ordinal))
                : Conversations;
            var changed = false;
            foreach (var message in owningConversations.SelectMany(conversation => conversation.Messages))
            {
                var trace = message.AgentTraceEntries.FirstOrDefault(entry =>
                    string.Equals(entry.CallId, action.AgentCallId, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(entry.ApprovalActionId)
                        && string.Equals(entry.ApprovalActionId, action.ActionId, StringComparison.OrdinalIgnoreCase)));
                if (trace == null)
                    continue;

                switch (action.Status)
                {
                    case ConfirmableActionStatus.Pending:
                    case ConfirmableActionStatus.Approved:
                        trace.State = CopilotToolExecutionState.AwaitingApproval;
                        break;
                    case ConfirmableActionStatus.Executing:
                        trace.State = CopilotToolExecutionState.Running;
                        message.MarkThinkingStarted();
                        message.IsExecutionInProgress = true;
                        break;
                    case ConfirmableActionStatus.Rejected:
                        trace.State = CopilotToolExecutionState.Denied;
                        trace.CompletedAtUtc = DateTimeOffset.UtcNow;
                        trace.ErrorMessage = "The user rejected this approval request.";
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                    case ConfirmableActionStatus.Expired:
                        trace.State = CopilotToolExecutionState.TimedOut;
                        trace.CompletedAtUtc = DateTimeOffset.UtcNow;
                        trace.ErrorMessage = "The approval request expired before a decision was recorded.";
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                    case ConfirmableActionStatus.Cancelled:
                        trace.State = CopilotToolExecutionState.Cancelled;
                        trace.CompletedAtUtc = action.CompletedAt ?? DateTimeOffset.UtcNow;
                        trace.ErrorMessage = CopilotAgentTraceEntry.Sanitize(action.ExecutionResultText);
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                    case ConfirmableActionStatus.Executed:
                        if (action.ResumesAgentOnApproval)
                            break;
                        trace.State = action.ExecutionSucceeded == true
                            ? CopilotToolExecutionState.Completed
                            : CopilotToolExecutionState.Failed;
                        trace.CompletedAtUtc = action.CompletedAt ?? DateTimeOffset.UtcNow;
                        trace.ResultSummary = action.ExecutionSucceeded == true
                            ? CopilotAgentTraceEntry.Sanitize(action.ExecutionResultText)
                            : trace.ResultSummary;
                        trace.ErrorMessage = action.ExecutionSucceeded == false
                            ? CopilotAgentTraceEntry.Sanitize(action.ExecutionResultText)
                            : string.Empty;
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                }

                trace.ApprovalActionId = action.ActionId;
                if (trace.CompletedAtUtc != null && trace.StartedAtUtc != default)
                    trace.DurationMs = Math.Max(trace.DurationMs, (long)Math.Max(0, (trace.CompletedAtUtc.Value - trace.StartedAtUtc).TotalMilliseconds));
                message.RebuildExecutionContentFromAgentTrace();
                changed = true;
            }

            if (changed)
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

            CopilotMcpConfirmationStore.Instance.Reject(
                action!.ActionId,
                CreateConfirmationReviewContext(),
                out var message);
            SetPendingActionFeedback($"{action.ActionId}: {message}");
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
