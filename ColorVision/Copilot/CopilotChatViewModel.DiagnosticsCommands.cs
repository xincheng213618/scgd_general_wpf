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
                PendingApprovals = _approvalCoordinator.TotalPendingCount,
            });
        }

        private string BuildEffectiveConfigDiagnosticsReport()
        {
            var stateStore = _stateStore as CopilotChatStateStore;
            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
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
                CodexConfigOptions = turnSnapshot.ProjectInstructionDiscoveryOptions,
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

            var hookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                _currentCodexConfigOptions.ConfiguredHooksEnabled,
                _currentCodexConfigOptions.ConfiguredPluginsEnabled,
                _currentCodexConfigOptions.ConfiguredCommandHooks);
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
                PendingApprovals = _approvalCoordinator.TotalPendingCount,
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
                && _followUpQueue.TryGet(run.RunId, out var queuedFollowUp)
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
                PendingActions = _approvalCoordinator.TotalPendingCount,
                RecentEntries = CopilotMcpAuditLogger.GetRecentEntries(verbose ? 20 : 8),
                LastError = CopilotMcpAuditLogger.GetLastError(),
                StatusMessage = server.LastStatusMessage,
                ExternalServers = externalServers,
            }, verbose);
        }
    }
}
