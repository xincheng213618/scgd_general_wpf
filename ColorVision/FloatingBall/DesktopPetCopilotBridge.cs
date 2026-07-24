using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.FloatingBall
{
    internal sealed class DesktopPetCopilotBridge
    {
        private readonly DesktopPetService _desktopPetService;
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
                    _desktopPetService.SetPendingCopilotAction(null, 0);
                    _desktopPetService.SetActivityState(DesktopPetActivityState.Idle);
                    return;
                }

                var pendingActions = CopilotMcpConfirmationStore.Instance.GetPendingActions();
                var pendingCount = pendingActions.Count;
                _lastPendingActionCount = pendingCount;
                if (pendingCount > 0)
                {
                    PublishPendingAction(pendingActions);
                    _desktopPetService.SetActivityState(DesktopPetActivityState.Waiting);
                    return;
                }

                _desktopPetService.SetPendingCopilotAction(null, 0);
                _desktopPetService.SetActivityState(
                    CopilotAgentTaskHost.Shared.IsActive
                        ? DesktopPetActivityState.Running
                        : DesktopPetActivityState.Idle);
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
                    case CopilotAgentTaskHostChangeKind.Started:
                    case CopilotAgentTaskHostChangeKind.CheckpointReady:
                        ApplyCurrentActiveState();
                        break;

                    case CopilotAgentTaskHostChangeKind.ControlRequested:
                        _desktopPetService.SetActivityState(DesktopPetActivityState.Waiting);
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

                var pendingActions = CopilotMcpConfirmationStore.Instance.GetPendingActions();
                var pendingCount = pendingActions.Count;
                if (pendingCount > 0)
                {
                    PublishPendingAction(pendingActions);
                    _desktopPetService.SetActivityState(DesktopPetActivityState.Waiting);
                    if (pendingCount > _lastPendingActionCount && DesktopPetConfig.Instance.ShowCopilotNotifications)
                    {
                        _desktopPetService.PlayTransientActivity(
                            DesktopPetActivityState.Waiting,
                            DesktopPetActivityState.Waiting);
                    }
                }
                else
                {
                    _desktopPetService.SetPendingCopilotAction(null, 0);
                    ApplyCurrentActiveState();
                }

                _lastPendingActionCount = pendingCount;
            });
        }

        private void HandleCompletedRun(CopilotHostedAgentRun run)
        {
            if (CopilotMcpConfirmationStore.Instance.PendingCount > 0)
            {
                _desktopPetService.SetActivityState(DesktopPetActivityState.Waiting);
                return;
            }

            if (CopilotAgentTaskHost.Shared.IsActive)
            {
                _desktopPetService.SetActivityState(DesktopPetActivityState.Running);
                return;
            }

            if (run.Completion.IsCanceled)
            {
                _desktopPetService.SetActivityState(DesktopPetActivityState.Idle);
                return;
            }

            var failed = run.Completion.IsFaulted;
            _desktopPetService.PlayTransientActivity(
                failed ? DesktopPetActivityState.Failed : DesktopPetActivityState.Review,
                DesktopPetActivityState.Idle);

            if (!DesktopPetConfig.Instance.ShowCopilotNotifications)
                return;

            _desktopPetService.Notify(
                "Copilot",
                failed
                    ? "任务执行失败，点击宠物打开 Copilot 查看详情。"
                    : "任务已经完成，点击宠物打开 Copilot 查看结果。",
                failed ? DesktopPetNotificationKind.Error : DesktopPetNotificationKind.Success);
        }

        private void ApplyCurrentActiveState()
        {
            if (CopilotMcpConfirmationStore.Instance.PendingCount > 0)
            {
                _desktopPetService.SetActivityState(DesktopPetActivityState.Waiting);
                return;
            }

            _desktopPetService.SetActivityState(
                CopilotAgentTaskHost.Shared.IsActive
                    ? DesktopPetActivityState.Running
                    : DesktopPetActivityState.Idle);
        }

        public static Task<CopilotConfirmationApprovalResult> ApproveAsync(
            ConfirmableAction action,
            CancellationToken cancellationToken)
        {
            return CopilotMcpConfirmationDecision.ApproveAsync(
                CopilotMcpConfirmationStore.Instance,
                action,
                cancellationToken);
        }

        public static bool Reject(ConfirmableAction action, out string message)
        {
            return CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, out message);
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
