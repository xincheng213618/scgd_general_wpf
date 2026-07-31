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
                    ShowLocalCommandResult(command, BuildAgentSkillDiagnosticsReport());
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
                    ArchiveCurrentConversation(command);
                    break;
                case CopilotLocalCommandKind.DeleteConversation:
                    DeleteCurrentConversation(command);
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
                _config.AgentDefaults.CreateSkillOverrideSnapshot());
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
            var result = CopilotPendingApprovalCommand.Evaluate(
                _pendingActions.Where(CanReviewPendingAction),
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
                ShowLocalCommandResult(
                    command,
                    CopilotSubagentDiagnostics.Format(SelectedConversation, arguments));
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

        private string BuildStatusDiagnosticsReport()
        {
            var profile = SelectedProfile;
            var defaults = _config.AgentDefaults;
            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            var activeRun = ActiveHostedRun;
            var conversation = SelectedConversation;
            var backgroundCommands =
                CopilotBackgroundShellCommandRegistry.Shared.GetSnapshots(conversation?.Id);
            var conversationMessages = conversation?.Messages
                ?.Where(message => message != null)
                .ToArray() ?? [];
            var latestAssistant = conversationMessages.LastOrDefault(message => !message.IsUser);
            var conversationRun = SelectedHostedRun;
            var branchOrigin = conversation?.BranchOrigin?.IsStructurallyValid(conversation.Id) == true
                ? conversation.BranchOrigin
                : null;
            var providerRetrySnapshot = activeRun?.ProviderRetrySnapshot
                ?? CopilotHostedProviderRetrySnapshot.Empty;
            var latestProviderRetry = providerRetrySnapshot.Latest;
            var providerRateLimits = CopilotProviderRateLimitTracker.GetSnapshot(profile?.Id);
            return CopilotStatusDiagnostics.Format(new CopilotStatusDiagnosticSnapshot
            {
                ApplicationVersion = CopilotStatusDiagnostics.FormatApplicationVersion(
                    typeof(CopilotChatViewModel).Assembly.GetName().Version),
                ProfileLabel = profile?.DisplayLabel ?? string.Empty,
                ProfileDetails = profile?.SecondaryLabel ?? string.Empty,
                ProfileConfigured = profile?.IsConfigured == true,
                ProviderFirstContentTimeoutSeconds = profile?.FirstContentTimeoutSeconds
                    ?? CopilotProfileConfig.DefaultFirstContentTimeoutSeconds,
                ProviderStreamingInactivityTimeoutSeconds = profile?.StreamingInactivityTimeoutSeconds
                    ?? CopilotProfileConfig.DefaultStreamingInactivityTimeoutSeconds,
                ProviderMaximumAttempts = CopilotProviderRetryChatClient.DefaultMaximumAttempts,
                ActiveProviderRetryCount = providerRetrySnapshot.Count,
                ActiveProviderRetryNextAttempt = latestProviderRetry?.NextAttempt ?? 0,
                ActiveProviderRetryMaximumAttempts = latestProviderRetry?.MaximumAttempts ?? 0,
                ActiveProviderRetryDelayMilliseconds = latestProviderRetry == null
                    ? 0
                    : (long)Math.Clamp(latestProviderRetry.Delay.TotalMilliseconds, 0, long.MaxValue),
                ActiveProviderRetryFailureKind = latestProviderRetry?.FailureKind ?? string.Empty,
                ActiveProviderRetryRequestId = latestProviderRetry?.RequestId ?? string.Empty,
                ProviderRateLimitCapturedAtUtc = providerRateLimits.CapturedAtUtc,
                ProviderRequestLimit = providerRateLimits.RequestLimit,
                ProviderRequestRemaining = providerRateLimits.RequestRemaining,
                ProviderRequestReset = providerRateLimits.RequestReset,
                ProviderTokenLimit = providerRateLimits.TokenLimit,
                ProviderTokenRemaining = providerRateLimits.TokenRemaining,
                ProviderTokenReset = providerRateLimits.TokenReset,
                ProviderProjectTokenLimit = providerRateLimits.ProjectTokenLimit,
                ProviderProjectTokenRemaining = providerRateLimits.ProjectTokenRemaining,
                ProviderProjectTokenReset = providerRateLimits.ProjectTokenReset,
                ProviderRateLimitRetryAfter = providerRateLimits.RetryAfter,
                ProviderRateLimitRequestId = providerRateLimits.RequestId,
                ReasoningLabel = profile?.ReasoningLabel ?? "默认",
                Mode = ResolveComposerRequestMode(),
                AgentState = activeRun?.State.ToString() ?? "Idle",
                QueuedAgentRuns = _taskHost.QueuedCount,
                MaximumQueuedAgentRuns = _taskHost.MaxQueuedRuns,
                HasConversation = conversation != null,
                ConversationTitle = conversation?.Title ?? string.Empty,
                ConversationId = conversation?.Id ?? string.Empty,
                ConversationVisibleTurns = conversationMessages.Count(message => message.IsUser),
                ConversationMessageCount = conversationMessages.Length,
                ConversationRunState = conversationRun?.State,
                ConversationQueuedFollowUps = QueuedFollowUps.Count(item => string.Equals(
                    item.ConversationId,
                    conversation?.Id,
                    StringComparison.Ordinal)),
                ConversationHasCheckpoint = conversation?.AgentSessionCheckpoint != null,
                ConversationHasRecoverableAgentTasks = latestAssistant?.HasRecoverableAgentTasks == true,
                ConversationIsBranch = branchOrigin != null,
                ConversationParentId = branchOrigin?.ParentConversationId ?? string.Empty,
                ConversationRootId = branchOrigin?.RootConversationId ?? string.Empty,
                WorkspacePath = turnSnapshot.SolutionDirectoryPath,
                ActiveDocumentPath = turnSnapshot.ActiveDocumentPath,
                AdditionalReadRootCount = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                    conversation?.AdditionalReadRootPaths).Length,
                BackgroundCommandCount = backgroundCommands.Count,
                ActiveBackgroundCommandCount = backgroundCommands.Count(item => item.IsActive),
                PreferredShell = defaults.PreferredShell,
                ContextWindowTokens = defaults.ContextWindowTokens,
                RequestTokenBudget = defaults.RequestTokenBudget,
                MaximumToolCalls = defaults.MaxToolCalls,
                MaximumAgentPasses = defaults.MaxAgentPasses,
                TimeoutSeconds = defaults.TimeoutSeconds,
                RegisteredCapabilities = capabilitySnapshot.Capabilities.Count,
                ApprovalCapabilities = capabilitySnapshot.Capabilities.Count(capability => capability.ApprovalMode != CopilotToolApprovalMode.Never),
                TrackedSkills = skillUsage.Entries.Count,
                ExplicitOnlySkills = skillUsage.HistoricalExplicitOnlySkills.Count,
                McpListenerEnabled = _config.McpEnabled,
                McpListenerRunning = CopilotMcpServer.Instance.IsRunning,
                EnabledExternalMcpServers = _config.ExternalMcpServers.Count(server => server?.Enabled == true),
                PendingApprovals = CopilotMcpConfirmationStore.Instance.PendingCount,
            });
        }

        private string BuildEffectiveConfigDiagnosticsReport()
        {
            var stateStore = _stateStore as CopilotChatStateStore;
            return CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
            {
                Config = _config,
                State = _state,
                Conversation = SelectedConversation,
                SelectedProfile = SelectedProfile,
                ComposerMode = ResolveComposerRequestMode(),
                ConfigFilePath = ConfigHandler.GetInstance().ConfigFilePath,
                StateFilePath = stateStore?.StateFilePath ?? string.Empty,
                StateLoadStatus = stateStore?.LastLoadStatus
                    ?? new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.NotAttempted),
                ConversationRunState = SelectedHostedRun?.State,
                McpListenerRunning = CopilotMcpServer.Instance.IsRunning,
            });
        }

        private string BuildDoctorDiagnosticsReport()
        {
            var profile = SelectedProfile;
            var enabledExternalMcpServers = _config.ExternalMcpServers
                .Where(server => server?.Enabled == true)
                .ToArray();
            var connectedExternalMcpServers = new List<string>();
            var unavailableExternalMcpServers = new List<string>();
            var changedExternalMcpServers = new List<string>();
            var uncheckedExternalMcpServers = new List<string>();
            foreach (var server in enabledExternalMcpServers)
            {
                if (!CopilotMcpClientHealthRegistry.TryGetSnapshot(server, out var health)
                    || health.State == CopilotMcpClientHealthState.Unknown)
                {
                    uncheckedExternalMcpServers.Add(server.Name);
                }
                else if (health.CacheInvalidated)
                {
                    changedExternalMcpServers.Add(server.Name);
                }
                else if (health.State == CopilotMcpClientHealthState.Connected)
                {
                    connectedExternalMcpServers.Add(server.Name);
                }
                else
                {
                    unavailableExternalMcpServers.Add(server.Name);
                }
            }

            var hookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot();
            var extensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            var recentHookFailureCount = CopilotToolExecutionAuditLogger.GetRecentEntries(30)
                .SelectMany(entry => entry.HookRuns ?? Array.Empty<CopilotToolExecutionHookRun>())
                .Count(run => run?.IsStructurallyValid() == true
                    && run.State is CopilotToolExecutionHookState.Failed or CopilotToolExecutionHookState.TimedOut);
            var recentMcpFailureCount = CopilotMcpAuditLogger.GetRecentEntries(20)
                .Count(entry => !entry.Success
                    && DateTimeOffset.UtcNow - entry.TimestampUtc <= RecentMcpFailureWindow);
            var skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            return CopilotDoctorDiagnostics.Format(new CopilotDoctorDiagnosticSnapshot
            {
                ProfileLabel = profile?.DisplayLabel ?? string.Empty,
                ProfileConfigured = profile?.IsConfigured == true,
                ProfileUsesInsecureHttp = profile != null && CopilotProviderEndpoint.Validate(profile).IsInsecureHttp,
                StatePersistenceNotice = StatePersistenceNoticeText,
                StatePersistenceBlocked = _stateStore is CopilotChatStateStore stateStore && stateStore.IsStatePersistenceBlocked,
                StateRecoveryNotice = StateRecoveryNoticeText,
                TaskHostShutdown = _taskHost.IsShutdown,
                QueuedAgentRuns = _taskHost.QueuedCount,
                MaximumQueuedAgentRuns = _taskHost.MaxQueuedRuns,
                McpListenerEnabled = _config.McpEnabled,
                McpListenerRunning = CopilotMcpServer.Instance.IsRunning,
                RecentMcpFailureCount = recentMcpFailureCount,
                EnabledExternalMcpServers = enabledExternalMcpServers.Length,
                ConnectedExternalMcpServers = connectedExternalMcpServers,
                UnavailableExternalMcpServers = unavailableExternalMcpServers,
                ChangedExternalMcpServers = changedExternalMcpServers,
                UncheckedExternalMcpServers = uncheckedExternalMcpServers,
                HookSurfaceValid = hookSurface.IsStructurallyValid(),
                EffectiveHookCount = hookSurface.Entries.Count,
                ExtensionSourceCount = extensionSnapshot.Sources.Count,
                ExtensionIssueCount = extensionSnapshot.Issues.Count,
                RecentHookFailureCount = recentHookFailureCount,
                TrackedSkillCount = skillUsage.Entries.Count,
                ExplicitOnlySkillCount = skillUsage.HistoricalExplicitOnlySkills.Count,
                PendingApprovals = CopilotMcpConfirmationStore.Instance.PendingCount,
            });
        }

        private void HandleTaskCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotTaskDiagnostics.ParseCommand(arguments);
            if (request.Action == CopilotTaskCommandAction.List)
            {
                ShowLocalCommandResult(command, BuildTaskDiagnosticsReport());
                return;
            }
            if (request.Action == CopilotTaskCommandAction.Invalid)
            {
                ShowLocalCommandResult(command, CopilotTaskDiagnostics.Usage);
                return;
            }

            var snapshot = CaptureTaskDiagnostics();
            if (request.Action == CopilotTaskCommandAction.Resume)
            {
                ResumeTaskFromCommand(command, snapshot, request.Position);
                return;
            }
            if (request.Action == CopilotTaskCommandAction.Dismiss)
            {
                DismissTaskFromCommand(command, snapshot, request.Position);
                return;
            }

            var run = CopilotTaskDiagnostics.FindRun(snapshot, request.Position);
            if (run == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"“活动与队列”中没有任务 #{request.Position:N0}。输入 /tasks 查看实时位置；任务可能已在后台变化。");
                return;
            }

            var confirmation = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                CopilotTaskDiagnostics.FormatStopConfirmation(run, request.Position),
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                ShowLocalCommandResult(command, $"停止任务 #{request.Position:N0} 已取消；所有任务保持不变。");
                return;
            }

            var pausedGoal = false;
            var outcome = CopilotTaskStopRequestOutcome.NotFound;
            if (run.State == CopilotHostedRunState.Queued
                && _queuedFollowUpsByRunId.TryGetValue(run.RunId, out var queuedFollowUp)
                && TryDeleteQueuedFollowUp(queuedFollowUp, out pausedGoal))
            {
                outcome = CopilotTaskStopRequestOutcome.CancelRequested;
            }
            else
            {
                outcome = CopilotTaskDiagnostics.RequestStop(_taskHost, run.RunId);
            }

            var report = outcome switch
            {
                CopilotTaskStopRequestOutcome.PauseRequested =>
                    $"已请求安全暂停任务 #{request.Position:N0}；可恢复 checkpoint 和既有审计证据会保留。",
                CopilotTaskStopRequestOutcome.CancelRequested when run.State == CopilotHostedRunState.Queued =>
                    $"已取消排队任务 #{request.Position:N0}；该请求不会执行，其他任务未改变。",
                CopilotTaskStopRequestOutcome.CancelRequested =>
                    $"已请求取消任务 #{request.Position:N0}；已完成消息与既有审计证据会保留。",
                _ => $"任务 #{request.Position:N0} 已完成、已在取消，或已离开原位置；未重复发出停止请求。",
            };
            if (pausedGoal)
                report += " 对应的活动持续目标也已暂停。";
            ShowLocalCommandResult(command, report);
        }

        private void HandleBackgroundShellCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotBackgroundShellCommandDiagnostics.ParseCommand(arguments);
            if (request.Action == CopilotBackgroundShellCommandAction.Invalid)
            {
                ShowLocalCommandResult(
                    command,
                    CopilotBackgroundShellCommandDiagnostics.Usage);
                return;
            }

            var conversation = SelectedConversation;
            AcknowledgeBackgroundCommandNotices(conversation?.Id);
            var snapshots = CopilotBackgroundShellCommandRegistry.Shared.GetSnapshots(
                conversation?.Id);
            if (request.Action == CopilotBackgroundShellCommandAction.List)
            {
                ShowLocalCommandResult(
                    command,
                    CopilotBackgroundShellCommandDiagnostics.FormatList(
                        conversation,
                        snapshots,
                        DateTimeOffset.UtcNow));
                return;
            }
            if (request.Action == CopilotBackgroundShellCommandAction.Clear)
            {
                var cleared = CopilotBackgroundShellCommandRegistry.Shared.ClearCompleted(
                    conversation?.Id);
                ShowLocalCommandResult(
                    command,
                    cleared == 0
                        ? "当前会话没有可清理的已结束后台命令；运行中的命令未改变。"
                        : $"已清理当前会话 {cleared:N0} 条结束记录；运行中的后台命令未改变。");
                return;
            }

            var snapshot = CopilotBackgroundShellCommandDiagnostics.Find(
                snapshots,
                request.Position);
            if (snapshot == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话没有后台命令 #{request.Position:N0}。输入 /ps 刷新列表；编号可能已随完成记录清理而变化。");
                return;
            }
            if (request.Action == CopilotBackgroundShellCommandAction.Inspect)
            {
                ShowLocalCommandResult(
                    command,
                    CopilotBackgroundShellCommandDiagnostics.FormatDetails(
                        snapshot,
                        request.Position,
                        DateTimeOffset.UtcNow));
                return;
            }
            if (!snapshot.IsActive)
            {
                ShowLocalCommandResult(
                    command,
                    $"后台命令 #{request.Position:N0} 已经是“{snapshot.State}”，没有重复发送停止请求。"
                    + Environment.NewLine
                    + Environment.NewLine
                    + CopilotBackgroundShellCommandDiagnostics.FormatDetails(
                        snapshot,
                        request.Position,
                        DateTimeOffset.UtcNow));
                return;
            }

            var confirmation = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                CopilotBackgroundShellCommandDiagnostics.FormatStopConfirmation(
                    snapshot,
                    request.Position),
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                ShowLocalCommandResult(
                    command,
                    $"停止后台命令 #{request.Position:N0} 已取消；进程树继续运行。");
                return;
            }

            RunUiOperation(
                () => StopBackgroundShellCommandAsync(
                    command,
                    conversation?.Id ?? string.Empty,
                    snapshot.Id,
                    request.Position),
                "停止后台命令");
        }

        private async Task StopBackgroundShellCommandAsync(
            CopilotLocalCommand command,
            string conversationId,
            string backgroundId,
            int position)
        {
            var result = await CopilotBackgroundShellCommandRegistry.Shared.StopAsync(
                conversationId,
                backgroundId,
                CancellationToken.None);
            if (!result.Success || result.Snapshot == null)
            {
                ShowLocalCommandResult(
                    command,
                    "后台命令未停止："
                    + (string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? "命令已经离开当前会话或状态刚刚变化。"
                        : result.ErrorMessage));
                return;
            }

            ShowLocalCommandResult(
                command,
                $"已停止后台命令 #{position:N0} 的进程树。"
                + Environment.NewLine
                + Environment.NewLine
                + CopilotBackgroundShellCommandDiagnostics.FormatDetails(
                    result.Snapshot,
                    position,
                    DateTimeOffset.UtcNow));
        }

        private void ResumeTaskFromCommand(
            CopilotLocalCommand command,
            CopilotTaskDiagnosticSnapshot snapshot,
            int position)
        {
            var attentionTask = CopilotTaskDiagnostics.FindAttentionTask(snapshot, position);
            if (attentionTask == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"“需要处理”中没有任务 #{position:N0}。输入 /tasks 查看实时位置；任务可能已恢复或离开列表。");
                return;
            }
            if (!attentionTask.CanResume)
            {
                ShowLocalCommandResult(
                    command,
                    $"任务 #{position:N0} 当前没有可用 checkpoint，不能直接恢复；原任务状态和审计证据未改变。");
                return;
            }

            var task = CopilotAgentTaskIndex.Build(
                    CopilotConversationArchiveService.GetActive(Conversations))
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.ConversationId, attentionTask.ConversationId, StringComparison.Ordinal));
            if (task == null || !TryResumeAgentTask(task))
            {
                ShowLocalCommandResult(
                    command,
                    $"任务 #{position:N0} 的 checkpoint、模型配置或运行环境刚刚变化，未启动恢复。请重新输入 /tasks 查看状态。");
                return;
            }

            ShowLocalCommandResult(
                command,
                $"已切换到“{attentionTask.Title}”并请求恢复任务 #{position:N0}；checkpoint 已重新验证，后续工具仍遵循现有审批策略。");
        }

        private void DismissTaskFromCommand(
            CopilotLocalCommand command,
            CopilotTaskDiagnosticSnapshot snapshot,
            int position)
        {
            var attentionTask = CopilotTaskDiagnostics.FindAttentionTask(snapshot, position);
            if (attentionTask == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"“需要处理”中没有任务 #{position:N0}。输入 /tasks 查看实时位置；任务可能已恢复或离开列表。");
                return;
            }
            if (IsBusy)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前仍有任务运行，不能放弃恢复项 #{position:N0}。请先停止或等待活动任务完成；恢复项未改变。");
                return;
            }

            var task = CopilotAgentTaskIndex.Build(
                    CopilotConversationArchiveService.GetActive(Conversations))
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.ConversationId, attentionTask.ConversationId, StringComparison.Ordinal));
            if (task == null || !TryDismissAgentTask(task))
            {
                ShowLocalCommandResult(
                    command,
                    $"未放弃恢复项 #{position:N0}；用户已取消确认，或任务状态刚刚变化。原任务终态和审计证据未改变。");
                return;
            }

            ShowLocalCommandResult(
                command,
                $"已放弃“{attentionTask.Title}”的恢复项 #{position:N0}；checkpoint 已清除，原任务终态、可见内容和审计证据仍保留。");
        }

        private CopilotTaskDiagnosticSnapshot CaptureTaskDiagnostics()
        {
            return CopilotTaskDiagnostics.Capture(
                _taskHost,
                CopilotConversationArchiveService.GetActive(Conversations),
                DateTimeOffset.UtcNow);
        }

        private string BuildTaskDiagnosticsReport()
        {
            return CopilotTaskDiagnostics.Format(CaptureTaskDiagnostics());
        }

        private void HandleMcpCommand(CopilotLocalCommand command, string arguments)
        {
            switch (CopilotMcpCommand.Resolve(arguments))
            {
                case CopilotMcpCommandAction.Summary:
                    ShowLocalCommandResult(command, BuildMcpDiagnosticsReport(verbose: false));
                    break;
                case CopilotMcpCommandAction.Verbose:
                    ShowLocalCommandResult(command, BuildMcpDiagnosticsReport(verbose: true));
                    break;
                default:
                    ShowLocalCommandResult(command, CopilotMcpCommand.Usage);
                    break;
            }
        }

        private string BuildMcpDiagnosticsReport(bool verbose)
        {
            var server = CopilotMcpServer.Instance;
            var externalServers = _config.ExternalMcpServers
                .Where(candidate => candidate?.Enabled == true)
                .Select(CopilotMcpDiagnostics.CaptureExternalServer)
                .ToArray();
            return CopilotMcpDiagnostics.Format(new CopilotMcpDiagnosticSnapshot
            {
                Endpoint = _config.McpEndpoint,
                Enabled = _config.McpEnabled,
                Running = server.IsRunning,
                PendingActions = CopilotMcpConfirmationStore.Instance.PendingCount,
                RecentEntries = CopilotMcpAuditLogger.GetRecentEntries(verbose ? 20 : 8),
                LastError = CopilotMcpAuditLogger.GetLastError(),
                StatusMessage = server.LastStatusMessage,
                ExternalServers = externalServers,
            }, verbose);
        }

        private void OpenComposerMention(CopilotLocalCommand command, string query)
        {
            if (!CopilotComposerReferenceCatalog.TryCreateMentionInput(
                    query,
                    out var mentionInput,
                    out var errorMessage))
            {
                ShowLocalCommandResult(
                    command,
                    $"{errorMessage}{Environment.NewLine}用法：{command.Usage}");
                return;
            }

            DismissLocalCommandResult();
            InputText = mentionInput;
        }

        private void StartWorkspaceReview(CopilotLocalCommand command, string focusInstructions)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再开始审查。");
                return;
            }

            var prompt = new StringBuilder("Review the current uncommitted workspace changes. Do not modify files or apply fixes.");
            if (!string.IsNullOrWhiteSpace(focusInstructions))
                prompt.Append(" Focus: ").Append(focusInstructions.Trim());

            DismissLocalCommandResult();
            SetPendingRequestModeOverride(CopilotAgentMode.Review);
            InputText = prompt.ToString();
            RunUiOperation(SendAsync, "开始工作区审查");
        }

        private void StartWorkspaceVerification(CopilotLocalCommand command, string focusInstructions)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再验证工作区。");
                return;
            }

            DismissLocalCommandResult();
            SetPendingRequestModeOverride(CopilotAgentMode.Review);
            InputText = CopilotWorkspaceVerification.BuildPrompt(focusInstructions);
            RunUiOperation(SendAsync, "验证工作区改动");
        }

        private void StartProjectInitialization(CopilotLocalCommand command)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再初始化项目指令。");
                return;
            }

            var workspaceRoot = CaptureHostedTurnSnapshot(Attachments).SolutionDirectoryPath;
            var plan = CopilotProjectInitialization.Create(workspaceRoot);
            if (!plan.CanStart)
            {
                ShowLocalCommandResult(command, plan.Message);
                return;
            }

            DismissLocalCommandResult();
            RunUiOperation(
                () => SendAsync(plan.VisiblePrompt, CopilotAgentMode.Code, plan.ModelPrompt),
                "初始化项目指令");
        }

        private void StartPlanRequest(CopilotLocalCommand command, string task)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再进入计划模式。");
                return;
            }

            SetPendingRequestModeOverride(CopilotAgentMode.Plan);
            if (string.IsNullOrWhiteSpace(task))
            {
                ShowLocalCommandResult(command, "下一条请求将使用计划模式：Copilot 只读取和分析相关证据，生成可执行计划，不会修改文件或应用状态。");
                return;
            }

            DismissLocalCommandResult();
            InputText = task.Trim();
            RunUiOperation(SendAsync, "生成执行计划");
        }

        private void ViewLatestCompletedPlan(CopilotLocalCommand command)
        {
            var plan = CopilotConversationPlanNavigation.FindLatestCompletedPlan(SelectedConversation);
            if (plan == null)
            {
                ShowLocalCommandResult(
                    command,
                    "当前会话没有已完成的计划。输入 /plan [任务] 可以先生成一份只读计划。");
                return;
            }

            DismissLocalCommandResult();
            MessageNavigationRequested?.Invoke(
                this,
                new CopilotChatMessageNavigationRequestedEventArgs(plan));
        }

        private void ManageConversationGoal(CopilotLocalCommand command, string arguments)
        {
            var conversation = SelectedConversation;
            if (conversation == null)
            {
                ShowLocalCommandResult(command, "当前没有可管理的会话。请先新建会话。");
                return;
            }

            var normalizedArguments = (arguments ?? string.Empty).Trim();
            if (IsBusy
                && normalizedArguments.Length > 0
                && !string.Equals(normalizedArguments, "pause", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedArguments, "clear", StringComparison.OrdinalIgnoreCase))
            {
                ShowLocalCommandResult(
                    command,
                    "当前 Agent 任务运行中；此时可以查看、暂停或清除持续目标。请在当前轮结束后再设置、编辑或恢复目标。");
                return;
            }

            var result = CopilotConversationGoalCommand.Execute(
                conversation.Goal,
                arguments,
                DateTimeOffset.UtcNow);
            if (result.Changed)
            {
                conversation.Goal = result.Goal;
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                RefreshComposerTokenEstimate();
            }

            ShowLocalCommandResult(command, result.Message);
            if (result.StartsWork && result.Goal?.IsActive == true)
            {
                RunUiOperation(
                    () => SendAsync(
                        result.Goal.Objective,
                        CopilotAgentMode.Auto,
                        result.Goal.Objective),
                    "执行持续目标");
            }
        }

        private async Task ShowGitDiffAsync(CopilotLocalCommand command, string scope)
        {
            if (_isInspectingGitDiff)
            {
                ShowLocalCommandResult(command, "Git 变更快照正在生成，请稍候。");
                return;
            }

            _isInspectingGitDiff = true;
            var cancellation = BeginAuxiliaryOperation();
            ShowLocalCommandResult(command, "正在读取本地 Git 变更…不会调用模型，也不会修改文件。");
            try
            {
                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                var searchRoots = CopilotAgentRequestFactory.BuildSearchRootPaths(turnSnapshot, Array.Empty<string>());
                var result = await _localGitDiffService.ExecuteAsync(searchRoots, scope, cancellation.Token);
                ShowLocalCommandResult(command, result.Report);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ShowLocalCommandResult(command, "Git 变更快照失败：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
            finally
            {
                CompleteAuxiliaryOperation(cancellation);
                _isInspectingGitDiff = false;
            }
        }

        private async Task CompactConversationAsync(
            CopilotLocalCommand command,
            string focusInstructions,
            bool includeFocusInResult = true)
        {
            var conversation = SelectedConversation;
            var profile = SelectedProfile;
            if (IsBusy || _isCompactingConversation)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再压缩上下文。");
                return;
            }
            if (conversation == null || profile?.IsConfigured != true)
            {
                ShowLocalCommandResult(command, "请先选择并配置可用模型。");
                return;
            }
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                CreateConversationRequestProfile(profile, conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot()))
            {
                var latestAssistant = conversation.Messages.LastOrDefault(message => message != null && !message.IsUser);
                var isFinalAnswerRecovery = latestAssistant?.HasRecoverableFinalAnswer == true;
                ShowLocalCommandResult(
                    command,
                    isFinalAnswerRecovery
                        ? "当前会话的 Agent 工作已完成，但最终回答尚未完整返回。请先使用“重试最终回答”，或在任务列表中明确放弃这条恢复项，再压缩上下文；本次压缩未开始，checkpoint 已保留。"
                        : $"当前会话还有可安全继续的 Agent 任务。请先使用“{latestAssistant?.AgentRecoveryActionLabel ?? "继续任务"}”处理它，或在任务列表中明确放弃它，再压缩上下文；本次压缩未开始，checkpoint 已保留。");
                return;
            }

            var sourceMessages = conversation.Messages
                .Where(message => !string.IsNullOrWhiteSpace(message.ModelContent))
                .ToArray();
            var newMessageCount = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
            if (sourceMessages.Length < 2 || newMessageCount < 2)
            {
                var reason = conversation.Compaction == null
                    ? "至少需要一轮完整对话后才能压缩。"
                    : "上次压缩后还没有足够的新对话，不需要重复压缩。";
                ShowLocalCommandResult(command, reason);
                return;
            }

            var summaryMaximumWeight = ResolveConversationHistoryLimits(profile).MaximumContentCharacters;
            var compactProfile = profile.Clone();
            compactProfile.UseSystemPromptOverride(CopilotConversationCompactionPrompt.SystemPrompt);
            compactProfile.MaxTokens = Math.Min(compactProfile.MaxTokens, CompactSummaryOutputTokens);
            compactProfile.Temperature = 0.1;

            var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(focusInstructions);
            var historyLimits = ResolveConversationHistoryLimits(compactProfile);
            compactProfile.MaxTokens = Math.Min(
                compactProfile.MaxTokens,
                ResolveCompactSummaryOutputTokens(summaryMaximumWeight));
            CopilotConversationCompactionPlan compactionPlan;
            try
            {
                compactionPlan = CopilotConversationCompactionPlanner.Create(conversation, historyLimits, compactRequest);
            }
            catch (Exception ex)
            {
                ShowLocalCommandResult(command, "压缩未开始：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
                return;
            }
            var request = compactionPlan.SourceMessages
                .Append(new CopilotRequestMessage("user", compactRequest))
                .ToArray();

            using var cancellation = new CopilotNonBlockingCancellationSource();
            _compactConversationCts = cancellation;
            _isCompactingConversation = true;
            IsBusy = true;
            ShowLocalCommandResult(command, "正在压缩当前对话…完整聊天记录会继续保留在本地。");
            try
            {
                var reply = await _chatService.CompleteReplyDetailedAsync(compactProfile, request, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (reply.IsIncomplete)
                    throw new InvalidOperationException(BuildIncompleteCompactionMessage(reply));
                var summary = NormalizeCompactSummary(reply.Content, summaryMaximumWeight);
                if (summary.Length == 0)
                    throw new InvalidOperationException("模型没有返回可用的压缩摘要。");
                compactionPlan.TerminalEvidence.EnsurePreserved(summary);
                if (!Conversations.Contains(conversation) || !conversation.Messages.Contains(compactionPlan.BoundaryMessage))
                    throw new InvalidOperationException("压缩期间会话已发生变化，结果未应用。");

                conversation.Compaction = new CopilotConversationCompaction
                {
                    StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
                    Summary = summary,
                    ThroughMessageId = compactionPlan.BoundaryMessage.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    SourceMessageCount = compactionPlan.TotalSourceMessageCount,
                    SourceCharacters = compactionPlan.TotalSourceCharacters,
                };
                conversation.AgentSessionCheckpoint = null;
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();

                var retainedAfterBoundary = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
                ShowLocalCommandResult(
                    command,
                    $"已将最早 {compactionPlan.NewSourceMessageCount:N0} 条完整上下文、{compactionPlan.NewSourceCharacters:N0} 个字符合并进延续摘要。\n"
                    + $"后续请求将使用 {summary.Length:N0} 字符摘要，并保留边界后的 {retainedAfterBoundary:N0} 条新消息；界面中的完整对话未删除。"
                    + (!includeFocusInResult || string.IsNullOrWhiteSpace(focusInstructions)
                        ? string.Empty
                        : "\n聚焦要求：" + focusInstructions.Trim()));
                RefreshComposerTokenEstimate();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                ShowLocalCommandResult(command, "上下文压缩已取消，原有对话和压缩状态均未改变。");
            }
            catch (Exception ex)
            {
                ShowLocalCommandResult(command, "压缩失败：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
            finally
            {
                if (ReferenceEquals(_compactConversationCts, cancellation))
                    _compactConversationCts = null;
                _isCompactingConversation = false;
                IsBusy = _taskHost.IsActive;
            }
        }

        private async Task<CopilotAutomaticCompactionOutcome> TryAutoCompactConversationAsync(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            string pendingPrompt)
        {
            if (IsBusy || _taskHost.IsActive || _isCompactingConversation || IsEditingMessage)
                return CopilotAutomaticCompactionOutcome.NotNeeded;
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                requestProfile,
                CopilotCapabilityCatalog.Shared.GetSnapshot()))
            {
                return CopilotAutomaticCompactionOutcome.NotNeeded;
            }

            var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
                conversation,
                ResolveConversationHistoryLimits(requestProfile),
                pendingPrompt,
                _config.AgentDefaults.AutoCompactConversationHistory,
                _config.AgentDefaults.AutoCompactThresholdPercent);
            if (!decision.ShouldCompact)
                return CopilotAutomaticCompactionOutcome.NotNeeded;

            var command = CopilotLocalCommandCatalog.FindExact("/compact");
            if (command == null)
                return CopilotAutomaticCompactionOutcome.Failed;

            var previousCompaction = conversation.Compaction;
            await CompactConversationAsync(
                command,
                CopilotConversationCompactionPrompt.BuildAutomaticFocus(
                    _config.AgentDefaults.AutoCompactInstructions),
                includeFocusInResult: false);
            var applied = !ReferenceEquals(previousCompaction, conversation.Compaction)
                && conversation.Compaction?.IsStructurallyValid() == true;
            if (!applied)
            {
                LocalCommandResultTitle = "自动压缩未完成";
                LocalCommandResultText = (LocalCommandResultText ?? string.Empty).Trim()
                    + Environment.NewLine
                    + "原请求尚未发送，输入和附件均已保留；请重试 /compact，或在设置中调整自动压缩策略。";
                return CopilotAutomaticCompactionOutcome.Failed;
            }

            var triggerText = decision.Trigger == CopilotConversationAutoCompactionTrigger.MessageCount
                ? $"消息数达到 {decision.UsagePercent:N0}%"
                : $"估算上下文达到 {decision.UsagePercent:N0}%";
            var customFocusText = _config.AgentDefaults.AutoCompactInstructions.Length > 0
                ? $"已应用 {_config.AgentDefaults.AutoCompactInstructions.Length:N0} 字符的自定义长期重点。"
                : "已应用内置默认保留重点。";
            LocalCommandResultTitle = "/compact · 自动压缩";
            LocalCommandResultText = $"{triggerText}，已在发送前自动压缩早期对话。"
                + Environment.NewLine
                + customFocusText
                + Environment.NewLine
                + LocalCommandResultText;
            return CopilotAutomaticCompactionOutcome.Applied;
        }

        private void CompactConversationFromUi()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/compact");
            if (command == null)
                return;

            RunUiOperation(() => CompactConversationAsync(command, string.Empty), "压缩上下文");
        }

        private static string NormalizeCompactSummary(string summary, int maximumWeight)
        {
            var normalized = (summary ?? string.Empty).Trim();
            if (normalized.Length > CopilotConversationCompaction.MaximumSummaryCharacters)
            {
                throw new InvalidOperationException(
                    $"模型返回的压缩摘要超过 {CopilotConversationCompaction.MaximumSummaryCharacters:N0} 字符安全上限，未应用结果。请缩小聚焦范围后重试。");
            }
            if (CopilotTokenEstimator.EstimateTextWeight(normalized) > maximumWeight)
            {
                throw new InvalidOperationException(
                    "模型返回的压缩摘要超过当前会话可安全保留的单条历史预算，未应用结果。请缩小聚焦范围后重试。");
            }

            return normalized;
        }

        private static int ResolveCompactSummaryOutputTokens(int maximumWeight)
        {
            return Math.Clamp(
                maximumWeight / CopilotTokenEstimator.AsciiCharactersPerToken,
                32,
                CompactSummaryOutputTokens);
        }

        private static string BuildIncompleteCompactionMessage(CopilotCompletedReplyResult reply)
        {
            if (reply.IsContentTruncated)
                return "压缩摘要超过应用可安全保留的长度，未应用不完整结果；请缩小聚焦范围后重试。";

            return reply.StreamResult.FinishKind switch
            {
                CopilotChatFinishKind.LengthLimit => "模型因输出长度上限提前结束，未应用不完整摘要；请缩小聚焦范围后重试。",
                CopilotChatFinishKind.ContentFiltered => "提供商的内容安全策略提前停止了压缩，未应用不完整摘要。",
                CopilotChatFinishKind.ToolRequested => "模型在压缩过程中请求了工具，未应用不完整摘要。",
                _ => "提供商未正常完成压缩，未应用不完整摘要。",
            };
        }

        private string BuildAgentSkillDiagnosticsReport()
        {
            var agentDefaults = _config.AgentDefaults;
            return CopilotAgentSkillDiagnostics.FormatReport(
                CopilotAgentSkillUsageStore.Shared.GetSnapshot(),
                CopilotAgentSkills.ResolveMetadataCharacterBudget(agentDefaults.ContextWindowTokens),
                agentDefaults.CreateSkillOverrideSnapshot());
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

        private static string BuildHookDiagnosticsReport()
        {
            var extensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            return CopilotHookDiagnostics.Format(new CopilotHookDiagnosticSnapshot
            {
                HookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot(),
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
            var requestProfile = selectedProfile == null
                ? null
                : CreateConversationRequestProfile(selectedProfile, conversation);
            var historyLimits = ResolveConversationHistoryLimits(requestProfile);
            var history = CopilotConversationRequestBuilder.CaptureHistorySelection(conversation, historyLimits);
            var projectInstructions = Array.Empty<CopilotProjectInstructionDocument>();
            var trustedProjectRoots = Array.Empty<string>();
            CopilotAgentSkillUsageSnapshot? skillUsage = null;
            if (agentContextEnabled)
            {
                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                trustedProjectRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(turnSnapshot).ToArray();
                projectInstructions = CopilotAgentProjectInstructions.Discover(
                    trustedProjectRoots,
                    turnSnapshot.ActiveDocumentPath,
                    turnSnapshot.Attachments
                        .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                        .Select(attachment => attachment.Value))
                    .ToArray();
                skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            }

            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var agentExtensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            var toolHookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot();
            var agentDefaults = _config.AgentDefaults;
            var retainedHistoryWeight = history.Messages.Sum(message => CopilotTokenEstimator.EstimateTextWeight(message.Content));
            var compaction = conversation?.Compaction;
            return CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                ProfileLabel = requestProfile?.DisplayLabel ?? string.Empty,
                Mode = mode,
                ResponsePersonality = conversation?.ResponsePersonality ?? CopilotResponsePersonality.None,
                SystemPromptCharacters = requestProfile?.EffectiveSystemPrompt.Length ?? 0,
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
                HistoryContextWindowTokens = agentDefaults.ContextWindowTokens,
                AutoCompactConversationHistory = agentDefaults.AutoCompactConversationHistory,
                AutoCompactThresholdPercent = agentDefaults.AutoCompactThresholdPercent,
                AutoCompactInstructionsCharacters = agentDefaults.AutoCompactInstructions.Length,
                CompactedSourceMessages = compaction?.SourceMessageCount ?? 0,
                CompactionSummaryCharacters = compaction?.Summary.Length ?? 0,
                ConversationGoalCharacters = conversation?.Goal?.Objective.Length ?? 0,
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
            if (!CopilotLocalFileLinkNavigator.TryResolve(document.Path, out var target)
                || !CopilotLocalFileLinkNavigator.TryOpen(target, out errorMessage))
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
            var documents = CopilotAgentProjectInstructions.Discover(
                trustedProjectRoots,
                turnSnapshot.ActiveDocumentPath,
                turnSnapshot.Attachments
                    .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                    .Select(attachment => attachment.Value));
            return new CopilotProjectInstructionSnapshot(
                trustedProjectRoots.Count > 0
                    ? trustedProjectRoots[0]
                    : turnSnapshot.SolutionDirectoryPath,
                turnSnapshot.ActiveDocumentPath,
                documents);
        }

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
