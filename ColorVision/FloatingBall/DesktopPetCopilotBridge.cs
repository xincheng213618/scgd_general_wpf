using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.FloatingBall
{
    internal sealed class DesktopPetCopilotBridge
    {
        private readonly DesktopPetService _desktopPetService;
        private readonly DesktopPetCopilotActivityTracker _activityTracker = new();
        private DispatcherTimer? _pendingActionRefreshTimer;
        private bool _isInitialized;
        private int _lastPendingActionCount;

        public DesktopPetCopilotBridge(DesktopPetService desktopPetService)
        {
            _desktopPetService = desktopPetService;
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            CopilotAgentTaskHost.Shared.Changed += TaskHost_Changed;
            CopilotMcpConfirmationStore.Instance.ActionsChanged += ConfirmationStore_ActionsChanged;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                _pendingActionRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(15),
                };
                _pendingActionRefreshTimer.Tick += (_, _) => RefreshState();
                _pendingActionRefreshTimer.Start();
            }
            RefreshState();
        }

        public void RefreshState()
        {
            RunOnUiThread(() =>
            {
                if (!DesktopPetConfig.Instance.EnableCopilotIntegration)
                {
                    _activityTracker.Clear();
                    _desktopPetService.SetPendingCopilotAction(null, 0);
                    _desktopPetService.SetCopilotActivities(Array.Empty<DesktopPetCopilotActivity>());
                    _desktopPetService.SetActivityState(DesktopPetActivityState.Idle);
                    return;
                }

                var pendingActions = GetVisiblePendingActions();
                _lastPendingActionCount = pendingActions.Count;
                ReconcileAndApply(pendingActions);
            });
        }

        public void MarkActivityViewed(string? conversationId)
        {
            RunOnUiThread(() =>
            {
                if (_activityTracker.MarkViewed(conversationId))
                    ReconcileAndApply();
            });
        }

        private void TaskHost_Changed(object? sender, CopilotAgentTaskHostChangedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (!DesktopPetConfig.Instance.EnableCopilotIntegration)
                    return;

                switch (e.Kind)
                {
                    case CopilotAgentTaskHostChangeKind.Queued:
                    case CopilotAgentTaskHostChangeKind.QueueChanged:
                    case CopilotAgentTaskHostChangeKind.Started:
                    case CopilotAgentTaskHostChangeKind.CheckpointReady:
                    case CopilotAgentTaskHostChangeKind.ControlRequested:
                        ReconcileAndApply();
                        break;

                    case CopilotAgentTaskHostChangeKind.Completed:
                        HandleCompletedRun(e.Run);
                        break;
                }
            });
        }

        private void ConfirmationStore_ActionsChanged(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (!DesktopPetConfig.Instance.EnableCopilotIntegration)
                    return;

                var pendingActions = GetVisiblePendingActions();
                var pendingCount = pendingActions.Count;
                ReconcileAndApply(pendingActions);
                if (pendingCount > _lastPendingActionCount && DesktopPetConfig.Instance.ShowCopilotNotifications)
                    _desktopPetService.PlayTransientActivity(DesktopPetActivityState.Waiting, DesktopPetActivityState.Waiting);
                _lastPendingActionCount = pendingCount;
            });
        }

        private void HandleCompletedRun(CopilotHostedAgentRun run)
        {
            var completionState = CopilotAgentRunActivityPolicy.ResolveCompletionState(run);
            _activityTracker.RecordCompletion(run.ConversationId, completionState);
            ReconcileAndApply();

            if (completionState is CopilotConversationActivityState.None or CopilotConversationActivityState.NeedsInput
                || !DesktopPetConfig.Instance.ShowCopilotNotifications)
            {
                return;
            }

            _desktopPetService.Notify(
                "Copilot",
                completionState == CopilotConversationActivityState.Blocked
                    ? "任务执行失败，点击宠物打开 Copilot 查看详情。"
                    : "任务已经完成，点击宠物打开 Copilot 查看结果。",
                completionState == CopilotConversationActivityState.Blocked
                    ? DesktopPetNotificationKind.Error
                    : DesktopPetNotificationKind.Success);
        }

        private void ReconcileAndApply(IReadOnlyList<ConfirmableAction>? pendingActions = null)
        {
            var activeRun = CopilotAgentTaskHost.Shared.ActiveRun;
            var actions = pendingActions ?? GetVisiblePendingActions();
            var activeNeedsInput = activeRun != null && actions.Count > 0;
            _activityTracker.ReconcileActive(activeRun?.ConversationId, activeNeedsInput);

            var activities = _activityTracker.Snapshot();
            var primaryActivity = activities.Count > 0 ? activities[0] : null;
            if (primaryActivity != null)
                _desktopPetService.SetCopilotConversation(primaryActivity.ConversationId);
            else if (activeRun != null)
                _desktopPetService.SetCopilotConversation(activeRun.ConversationId);

            _desktopPetService.SetCopilotActivities(activities);
            if (actions.Count > 0)
                PublishPendingAction(actions);
            else
                _desktopPetService.SetPendingCopilotAction(null, 0);

            if (actions.Count > 0 && activeRun == null)
            {
                _desktopPetService.SetActivityState(DesktopPetActivityState.Waiting);
                return;
            }

            _desktopPetService.SetActivityState(primaryActivity?.PetState ?? DesktopPetActivityState.Idle);
        }

        public static Task<CopilotConfirmationApprovalResult> ApproveAsync(
            ConfirmableAction action,
            CancellationToken cancellationToken)
        {
            return CopilotMcpConfirmationDecision.ApproveAsync(
                CopilotMcpConfirmationStore.Instance,
                action,
                CreateReviewContext(action),
                cancellationToken);
        }

        public static bool Reject(ConfirmableAction action, out string message)
        {
            return CopilotMcpConfirmationStore.Instance.Reject(
                action.ActionId,
                CreateReviewContext(action),
                out message);
        }

        private static IReadOnlyList<ConfirmableAction> GetVisiblePendingActions()
        {
            return CopilotMcpConfirmationStore.Instance.GetPendingActionsForConversation(
                CopilotAgentTaskHost.Shared.ActiveRun?.ConversationId);
        }

        private static CopilotConfirmationReviewContext CreateReviewContext(ConfirmableAction action)
        {
            var activeRun = CopilotAgentTaskHost.Shared.ActiveRun;
            var isOwningRun = activeRun != null
                && action.RequestContext.SourceKind == CopilotApprovalSourceKind.InAppAgent
                && string.Equals(activeRun.ConversationId, action.RequestContext.ConversationId, StringComparison.Ordinal)
                && string.Equals(activeRun.Id, action.RequestContext.TaskId, StringComparison.Ordinal);
            return new CopilotConfirmationReviewContext(
                isOwningRun ? activeRun!.ConversationId : string.Empty,
                isOwningRun ? activeRun!.Id : string.Empty,
                SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty);
        }

        private void PublishPendingAction(System.Collections.Generic.IReadOnlyList<ConfirmableAction> pendingActions)
        {
            var shouldShowCard = DesktopPetConfig.Instance.ShowNotifications
                && DesktopPetConfig.Instance.ShowCopilotNotifications;
            _desktopPetService.SetPendingCopilotAction(
                shouldShowCard ? pendingActions[0] : null,
                shouldShowCard ? pendingActions.Count : 0);
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
    }
}
