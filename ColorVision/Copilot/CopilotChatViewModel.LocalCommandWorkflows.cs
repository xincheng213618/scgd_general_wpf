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
        private bool TryExecuteLocalCommand(string prompt)
        {
            var invocation = CopilotLocalCommandCatalog.Parse(prompt);
            if (invocation == null)
                return false;

            InputText = string.Empty;
            var command = invocation.Command;
            switch (command.Kind)
            {
                case CopilotLocalCommandKind.Help:
                    ShowLocalCommandResult(command, CopilotLocalCommandHelp.Format(invocation.Arguments));
                    break;
                case CopilotLocalCommandKind.Shortcuts:
                    ShowLocalCommandResult(command, CopilotKeyboardShortcutHelp.Format());
                    break;
                case CopilotLocalCommandKind.Recap:
                    ShowLocalCommandResult(
                        command,
                        CopilotConversationRecap.Format(
                            SelectedConversation,
                            QueuedFollowUps.Count(item => string.Equals(
                                item.ConversationId,
                                SelectedConversation?.Id,
                                StringComparison.Ordinal))));
                    break;
                case CopilotLocalCommandKind.Status:
                    ShowLocalCommandResult(command, BuildStatusDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.EffectiveConfig:
                    ShowLocalCommandResult(command, BuildEffectiveConfigDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Doctor:
                    ShowLocalCommandResult(command, BuildDoctorDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Feedback:
                    RunUiOperation(
                        () => OpenFeedbackAsync(invocation.Arguments),
                        "打开反馈");
                    break;
                case CopilotLocalCommandKind.Tasks:
                    HandleTaskCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.BackgroundCommands:
                    HandleBackgroundShellCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.TaskLog:
                    ShowLocalCommandResult(
                        command,
                        CopilotAgentTaskEventDiagnostics.Format(SelectedConversation, invocation.Arguments));
                    break;
                case CopilotLocalCommandKind.Queue:
                    HandleQueuedFollowUpCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.StopTask:
                    StopTaskFromCommand(command);
                    break;
                case CopilotLocalCommandKind.Approve:
                    HandlePendingApprovalCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Usage:
                    ShowLocalCommandResult(
                        command,
                        CopilotUsageCommand.Format(
                            SelectedConversation,
                            Conversations,
                            DateTimeOffset.Now,
                            invocation.Arguments,
                            CopilotProviderRateLimitTracker.GetSnapshot(SelectedProfile?.Id),
                            invocation.InvokedName));
                    break;
                case CopilotLocalCommandKind.Subagents:
                    HandleSubagentCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Context:
                    ShowLocalCommandResult(command, BuildContextDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.ProjectInstructions:
                    HandleProjectInstructionCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Permissions:
                    HandlePermissionsCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.AdditionalDirectories:
                    HandleAdditionalDirectoryCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Settings:
                    OpenSettingsFromCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.InitializeProject:
                    StartProjectInitialization(command);
                    break;
                case CopilotLocalCommandKind.Hooks:
                    ShowLocalCommandResult(command, BuildHookDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Skills:
                    HandleAgentSkillsCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Mcp:
                    HandleMcpCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Mention:
                    OpenComposerMention(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Diff:
                    RunUiOperation(() => ShowGitDiffAsync(command, invocation.Arguments), "读取 Git 变更");
                    break;
                case CopilotLocalCommandKind.RollbackWorkspace:
                    RollbackWorkspaceFromCommand(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Compact:
                    RunUiOperation(() => CompactConversationAsync(command, invocation.Arguments), "压缩上下文");
                    break;
                case CopilotLocalCommandKind.Review:
                    StartWorkspaceReview(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Verify:
                    StartWorkspaceVerification(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Plan:
                    StartPlanRequest(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.ViewPlan:
                    ViewLatestCompletedPlan(command);
                    break;
                case CopilotLocalCommandKind.Goal:
                    ManageConversationGoal(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.ResumeConversation:
                    ResumeConversation(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.ArchiveConversation:
                    RunUiOperation(
                        () => ArchiveCurrentConversationAsync(command),
                        "归档会话");
                    break;
                case CopilotLocalCommandKind.DeleteConversation:
                    RunUiOperation(
                        () => DeleteCurrentConversationAsync(command),
                        "删除会话");
                    break;
                case CopilotLocalCommandKind.UnarchiveConversation:
                    UnarchiveConversation(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.RenameConversation:
                    RenameCurrentConversation(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.RewindConversation:
                    RewindConversation(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.NavigateTurn:
                    NavigateToConversationTurn(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.SearchPromptHistory:
                    OpenPromptHistorySearch(command);
                    break;
                case CopilotLocalCommandKind.PromptSuggestions:
                    ChangePromptSuggestionPreference(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Transcript:
                    ChangeTranscriptExpansion(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.Timestamps:
                    ChangeMessageTimestampVisibility(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.CompactMode:
                    ChangeCompactMessageLayout(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.MultilineComposer:
                    ChangeMultilineComposerPreference(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.FollowUpBehavior:
                    ChangeFollowUpBehavior(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.RetryResponse:
                    RetryLatestResponse(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.CopyResponse:
                    CopyAssistantResponse(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.ExportConversation:
                    RunUiOperation(
                        () => ExportConversationFromCommandAsync(command, invocation.Arguments),
                        "导出会话");
                    break;
                case CopilotLocalCommandKind.FindInConversation:
                    OpenConversationFind(invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.SelectModel:
                    SelectModelProfile(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.SelectReasoning:
                    SelectReasoningMode(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.SelectPersonality:
                    SelectResponsePersonality(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.ClearConversation:
                    ClearConversationContext(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.ForkConversation:
                    ForkCurrentConversation(command, invocation.Arguments);
                    break;
                default:
                    return false;
            }
            return true;
        }

        private IReadOnlyList<CopilotAgentSkillCatalogItem> DiscoverComposerSkills()
        {
            if (ResolveComposerRequestMode() == CopilotAgentMode.Chat)
                return Array.Empty<CopilotAgentSkillCatalogItem>();

            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var trustedProjectRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(turnSnapshot);
            return CopilotAgentSkillCatalog.DiscoverCached(
                trustedProjectRoots,
                _config.AgentDefaults.CreateSkillOverrideSnapshot(),
                applicationBaseDirectory: null,
                userProfileDirectory: null,
                activeDocumentPath: turnSnapshot.ActiveDocumentPath,
                pathOverrides: _config.AgentDefaults.CreateSkillPathOverrideSnapshot());
        }

        private bool TryReportCommandInputRecovery(string prompt)
        {
            var normalized = (prompt ?? string.Empty).TrimStart();
            if (normalized.Length == 0 || normalized[0] is not '/' and not '$')
                return false;

            if (!CopilotCommandInputRecoveryResolver.TryResolve(
                prompt,
                DiscoverComposerSkills(),
                out var recovery))
            {
                return false;
            }

            LocalCommandResultTitle = recovery.Title;
            LocalCommandResultText = recovery.Message;
            return true;
        }

        private void OpenPromptHistorySearch(CopilotLocalCommand command)
        {
            DismissLocalCommandResult();
            if (TryOpenPromptHistorySearch())
                return;

            ShowLocalCommandResult(
                command,
                IsBusy
                    ? "请先等待当前任务结束或停止任务，再搜索历史请求。"
                    : "当前会话没有可搜索的可见历史请求。");
        }

        private void NavigateToConversationTurn(
            CopilotLocalCommand command,
            string requestedOrdinal)
        {
            var result = CopilotConversationTurnNavigation.Resolve(
                SelectedConversation,
                requestedOrdinal);
            if (result.Message == null)
            {
                ShowLocalCommandResult(command, result.Report);
                return;
            }

            DismissLocalCommandResult();
            MessageNavigationRequested?.Invoke(
                this,
                new CopilotChatMessageNavigationRequestedEventArgs(result.Message));
        }

        private void ChangeTranscriptExpansion(
            CopilotLocalCommand command,
            string arguments)
        {
            var result = CopilotConversationTranscriptExpansion.Execute(
                SelectedConversation,
                arguments);
            if (result.ChangedMessageCount > 0)
                PersistState();

            ShowLocalCommandResult(command, result.Report);
        }

        private void HandlePendingApprovalCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            RefreshPendingActions();
            var reviewableActions = _pendingActions.Where(CanReviewPendingAction).ToArray();
            if (reviewableActions.Length == 0)
            {
                var conversation = SelectedConversation;
                var workspacePath = CaptureHostedTurnSnapshot(
                    conversation?.Attachments ?? Enumerable.Empty<CopilotAttachmentItem>()).SolutionDirectoryPath;
                var denialResult = CopilotAutomaticApprovalDenialCommand.Evaluate(
                    CopilotAutomaticApprovalOverrideStore.Shared.GetRecentDenials(
                        conversation?.Id,
                        workspacePath),
                    arguments,
                    DateTimeOffset.UtcNow);
                if (!denialResult.AuthorizesRetry)
                {
                    ShowLocalCommandResult(command, denialResult.Report);
                    return;
                }

                if (!CopilotAutomaticApprovalOverrideStore.Shared.TryAuthorizeOneRetry(
                    denialResult.Denial!.DenialId,
                    conversation?.Id,
                    workspacePath,
                    out var authorizedDenial))
                {
                    ShowLocalCommandResult(
                        command,
                        "该拒绝记录已被使用、已过期，或当前会话与工作区不再匹配；没有创建重试授权。");
                    return;
                }

                RunUiOperation(
                    () => RetryAutomaticallyDeniedActionAsync(authorizedDenial),
                    "精确重试自动审查拒绝操作");
                return;
            }

            var result = CopilotPendingApprovalCommand.Evaluate(
                reviewableActions,
                arguments,
                DateTimeOffset.UtcNow);
            if (!result.OpensReview)
            {
                ShowLocalCommandResult(command, result.Report);
                return;
            }

            RunUiOperation(
                () => ApprovePendingActionAsync(result.Action),
                "审核待确认操作");
        }

        private void HandleSubagentCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotSubagentDiagnostics.ParseCommand(arguments);
            if (request.Action == CopilotSubagentDiagnosticAction.Steer)
            {
                var steeringResult = CopilotSubagentCoordination.RequestSteerActiveRun(
                    SelectedConversation?.Id,
                    request.RunId,
                    request.Message);
                ShowLocalCommandResult(
                    command,
                    CopilotSubagentDiagnostics.FormatSteeringResult(request.RunId, steeringResult));
                return;
            }
            if (request.Action == CopilotSubagentDiagnosticAction.Close)
            {
                var closeResult = CopilotSubagentDiagnostics.CloseRun(
                    SelectedConversation,
                    request.RunId);
                if (closeResult == CopilotSubagentCloseResult.Closed)
                {
                    PersistState(immediate: true);
                    RefreshLocalCommandSuggestions();
                }
                if (closeResult is CopilotSubagentCloseResult.Closed
                    or CopilotSubagentCloseResult.AlreadyClosed)
                {
                    AcknowledgeSubagentCompletionNotice(
                        SelectedConversation?.Id,
                        request.RunId);
                }
                ShowLocalCommandResult(
                    command,
                    CopilotSubagentDiagnostics.FormatCloseResult(request.RunId, closeResult));
                return;
            }
            if (request.Action != CopilotSubagentDiagnosticAction.Stop)
            {
                if (request.Action is CopilotSubagentDiagnosticAction.Overview
                    or CopilotSubagentDiagnosticAction.Runs
                    or CopilotSubagentDiagnosticAction.Done)
                {
                    AcknowledgeSubagentCompletionNotices(SelectedConversation?.Id);
                }
                else if (request.Action == CopilotSubagentDiagnosticAction.Show)
                {
                    AcknowledgeSubagentCompletionNotice(
                        SelectedConversation?.Id,
                        request.RunId);
                }
                var showsRoles = request.Action is CopilotSubagentDiagnosticAction.Overview
                    or CopilotSubagentDiagnosticAction.Roles;
                var usesActiveRequestSnapshot = showsRoles && ActiveHostedRun?.IsAgent == true;
                var customAgentOptions = showsRoles
                    ? usesActiveRequestSnapshot
                        ? _currentCodexConfigOptions
                        : CaptureHostedTurnSnapshot(Attachments).ProjectInstructionDiscoveryOptions
                    : CopilotProjectInstructionDiscoveryConfig.CreateDefault();
                ShowLocalCommandResult(
                    command,
                    CopilotSubagentDiagnostics.Format(
                        SelectedConversation,
                        arguments,
                        customSubagents: customAgentOptions.CustomSubagents,
                        customAgentsEnabled: customAgentOptions.EffectiveAgentsEnabled,
                        customAgentSnapshotLabel: usesActiveRequestSnapshot
                            ? "当前活动 Agent 请求的提交快照"
                            : "下一次 Agent 请求的当前配置快照"));
                return;
            }

            var result = CopilotSubagentCoordination.RequestCancelActiveRun(
                SelectedConversation?.Id,
                request.RunId);
            ShowLocalCommandResult(
                command,
                CopilotSubagentDiagnostics.FormatCancelResult(request.RunId, result));
        }

        private void HandleQueuedFollowUpCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotQueuedFollowUpDiagnostics.ParseCommand(arguments);
            if (request.Action == CopilotQueuedFollowUpCommandAction.List)
            {
                ShowLocalCommandResult(command, CopilotQueuedFollowUpDiagnostics.Format(
                    QueuedFollowUps,
                    SelectedConversation?.Id));
                return;
            }
            if (request.Action == CopilotQueuedFollowUpCommandAction.Invalid)
            {
                ShowLocalCommandResult(command, CopilotQueuedFollowUpDiagnostics.Usage);
                return;
            }
            if (request.Action == CopilotQueuedFollowUpCommandAction.Clear)
            {
                ClearQueuedFollowUpsFromCommand(command);
                return;
            }

            var queuedFollowUp = CopilotQueuedFollowUpDiagnostics.FindByPosition(
                QueuedFollowUps,
                SelectedConversation?.Id,
                request.QueuePosition);
            if (queuedFollowUp == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话没有全局队列位置 #{request.QueuePosition:N0}。输入 /queue 查看实时位置；队列可能已在后台变化。");
                return;
            }

            var originalPosition = queuedFollowUp.QueuePosition;
            switch (request.Action)
            {
                case CopilotQueuedFollowUpCommandAction.SendNow:
                    if (!TrySendQueuedFollowUpNow(queuedFollowUp))
                    {
                        ShowLocalCommandResult(command, $"当前没有可安全停止的前序任务，#{originalPosition:N0} 未提升。");
                        return;
                    }
                    ShowLocalCommandResult(
                        command,
                        $"已把原 #{originalPosition:N0} 提升为下一项，并请求停止当前任务；该请求会在当前任务收尾后开始。");
                    break;
                case CopilotQueuedFollowUpCommandAction.Edit:
                    if (!TryEditQueuedFollowUp(queuedFollowUp))
                    {
                        var reason = queuedFollowUp.IsAutomaticGoalContinuation
                            ? "自动持续目标续作不能转成手动草稿；可用 delete 取消并暂停目标。"
                            : "请先退出消息编辑，并清空当前草稿、附件及目标会话草稿。";
                        ShowLocalCommandResult(command, $"无法编辑 #{originalPosition:N0}。{reason}");
                        return;
                    }
                    ShowLocalCommandResult(
                        command,
                        $"已取消原 #{originalPosition:N0}，并把请求模式、正文和附件快照恢复到输入框；不会自动发送。");
                    break;
                case CopilotQueuedFollowUpCommandAction.MoveUp:
                case CopilotQueuedFollowUpCommandAction.MoveDown:
                    var offset = request.Action == CopilotQueuedFollowUpCommandAction.MoveUp ? -1 : 1;
                    if (!TryMoveQueuedFollowUp(queuedFollowUp, offset))
                    {
                        var boundary = offset < 0 ? "最前" : "最后";
                        ShowLocalCommandResult(command, $"#{originalPosition:N0} 已在队列{boundary}，或位置刚刚变化；队列未修改。");
                        return;
                    }
                    ShowLocalCommandResult(
                        command,
                        $"已把原 #{originalPosition:N0} 移动到 #{queuedFollowUp.QueuePosition:N0}；持久化恢复顺序已同步。");
                    break;
                case CopilotQueuedFollowUpCommandAction.Delete:
                    if (!TryDeleteQueuedFollowUp(queuedFollowUp, out var pausedGoal))
                    {
                        ShowLocalCommandResult(command, $"#{originalPosition:N0} 已开始执行或已离开队列，未重复取消。");
                        return;
                    }
                    ShowLocalCommandResult(
                        command,
                        $"已取消原 #{originalPosition:N0}，其请求不会执行。"
                        + (pausedGoal ? " 对应持续目标也已暂停。" : string.Empty));
                    break;
            }
        }

        private void ClearQueuedFollowUpsFromCommand(CopilotLocalCommand command)
        {
            var conversation = SelectedConversation;
            var queuedFollowUps = CopilotQueuedFollowUpDiagnostics.GetItems(
                QueuedFollowUps,
                conversation?.Id);
            if (conversation == null || queuedFollowUps.Count == 0)
            {
                ShowLocalCommandResult(command, "当前会话没有排队的后续请求，队列未修改。");
                return;
            }

            var confirmation = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                CopilotQueuedFollowUpDiagnostics.FormatClearConfirmation(
                    conversation.Title,
                    queuedFollowUps),
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                ShowLocalCommandResult(command, "清空队列已取消；所有排队请求和持续目标均保持不变。");
                return;
            }

            var cancelled = 0;
            var pausedGoals = 0;
            foreach (var queuedFollowUp in queuedFollowUps)
            {
                if (!TryDeleteQueuedFollowUp(queuedFollowUp, out var pausedGoal))
                    continue;

                cancelled++;
                if (pausedGoal)
                    pausedGoals++;
            }

            var failed = queuedFollowUps.Count - cancelled;
            var builder = new StringBuilder()
                .Append("已取消当前会话 ")
                .Append(cancelled.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(queuedFollowUps.Count.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 条排队请求；其他会话队列未改变。");
            if (pausedGoals > 0)
            {
                builder.Append("已暂停 ")
                    .Append(pausedGoals.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 个仍活动的对应持续目标。");
            }
            if (failed > 0)
            {
                builder.Append("另有 ")
                    .Append(failed.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 条已开始执行或位置刚刚变化，未重复取消。");
            }
            ShowLocalCommandResult(command, builder.ToString().TrimEnd());
        }
    }
}
