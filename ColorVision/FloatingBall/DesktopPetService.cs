#pragma warning disable CA1822
using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.FloatingBall
{
    public class DesktopPetService
    {
        private static readonly Lazy<DesktopPetService> LazyInstance = new(() => new DesktopPetService());
        private readonly DesktopPetCopilotBridge _copilotBridge;
        private FloatingBallWindow? _window;
        private DesktopPetSettingsWindow? _settingsWindow;
        private DesktopPetActivityState _activityState = DesktopPetActivityState.Idle;
        private ConfirmableAction? _pendingCopilotAction;
        private int _pendingCopilotActionCount;

        private DesktopPetService()
        {
            _copilotBridge = new DesktopPetCopilotBridge(this);
        }

        public static DesktopPetService GetInstance() => LazyInstance.Value;

        public bool IsVisible => _window != null && _window.IsVisible;

        public FloatingBallWindow Show()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
                return Application.Current.Dispatcher.Invoke(Show);

            _copilotBridge.Initialize();
            if (_window == null)
            {
                _window = new FloatingBallWindow();
            }

            _window.Show();
            _window.SetActivityState(_activityState);
            _window.ShowCopilotConfirmation(_pendingCopilotAction, _pendingCopilotActionCount);
            return _window;
        }

        public void Hide()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(Hide);
                return;
            }

            if (_window != null)
            {
                _window.CloseFromConfig();
                _window = null;
            }
        }

        public void Toggle()
        {
            MainWindowConfig.Instance.OpenFloatingBall = !MainWindowConfig.Instance.OpenFloatingBall;
        }

        public void Detach(FloatingBallWindow window)
        {
            if (ReferenceEquals(_window, window))
                _window = null;
        }

        public void ReloadSelectedAsset()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(ReloadSelectedAsset);
                return;
            }

            _window?.ReloadSelectedAsset();
        }

        public void SetActivityState(DesktopPetActivityState state)
        {
            _activityState = state;
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(() => SetActivityState(state));
                return;
            }

            _window?.SetActivityState(state);
        }

        public void PlayTransientActivity(DesktopPetActivityState state, DesktopPetActivityState returnState)
        {
            _activityState = returnState;
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(() => PlayTransientActivity(state, returnState));
                return;
            }

            _window?.PlayTransientActivity(state, returnState);
        }

        public void RefreshCopilotIntegration()
        {
            _copilotBridge.Initialize();
            _copilotBridge.RefreshState();
        }

        internal void SetPendingCopilotAction(ConfirmableAction? action, int totalPending)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(() => SetPendingCopilotAction(action, totalPending));
                return;
            }

            _pendingCopilotAction = action;
            _pendingCopilotActionCount = action == null ? 0 : Math.Max(1, totalPending);
            _window?.ShowCopilotConfirmation(_pendingCopilotAction, _pendingCopilotActionCount);
        }

        internal Task<CopilotConfirmationApprovalResult> ApproveCopilotActionAsync(
            ConfirmableAction action,
            CancellationToken cancellationToken)
        {
            return DesktopPetCopilotBridge.ApproveAsync(action, cancellationToken);
        }

        internal bool RejectCopilotAction(ConfirmableAction action, out string message)
        {
            return DesktopPetCopilotBridge.Reject(action, out message);
        }

        public void Notify(string title, string message, DesktopPetNotificationKind kind = DesktopPetNotificationKind.Info)
        {
            if (!DesktopPetConfig.Instance.ShowNotifications || !MainWindowConfig.Instance.OpenFloatingBall)
                return;

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => Notify(title, message, kind));
                return;
            }

            var window = Show();
            window.EnqueueNotification(new DesktopPetNotification
            {
                Title = title,
                Message = message,
                Kind = kind
            });
        }

        public void ShowStartupGreeting()
        {
            if (!DesktopPetConfig.Instance.ShowStartupGreeting || !MainWindowConfig.Instance.OpenFloatingBall)
                return;

            Notify(DesktopPetConfig.Instance.PetName, Properties.Resources.DesktopPetStartupGreeting, DesktopPetNotificationKind.Success);
            PlayTransientActivity(DesktopPetActivityState.Waving, _activityState);
        }

        public void ShowMainWindow()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(ShowMainWindow);
                return;
            }

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return;

            if (!mainWindow.IsVisible)
                mainWindow.Show();

            if (mainWindow.WindowState == WindowState.Minimized)
                mainWindow.WindowState = WindowState.Normal;

            mainWindow.Activate();
        }

        public void OpenSettings()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(OpenSettings);
                return;
            }

            if (_settingsWindow != null)
            {
                if (_settingsWindow.WindowState == WindowState.Minimized)
                    _settingsWindow.WindowState = WindowState.Normal;
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new DesktopPetSettingsWindow
            {
                Owner = Application.Current.GetActiveWindow()
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        public void OpenAdvancedSettings(Window? owner = null)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OpenAdvancedSettings(owner));
                return;
            }

            var window = new PropertyEditorWindow(DesktopPetConfig.Instance)
            {
                Title = Properties.Resources.DesktopPetSettingsTitle,
                Owner = owner ?? Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Submited += (_, _) =>
            {
                ConfigHandler.GetInstance().Save<DesktopPetConfig>();
                ReloadSelectedAsset();
                RefreshCopilotIntegration();
            };
            window.Show();
        }

        public void OpenCopilot()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(OpenCopilot);
                return;
            }

            ShowMainWindow();
            CopilotPanelService.GetInstance().ShowPanel();
        }
    }

    public class DesktopPetInitializer : MainWindowInitializedBase
    {
        public override string Name => Properties.Resources.DesktopPetInitializerName;
        public override int Order { get; set; } = 1000;

        public override System.Threading.Tasks.Task Initialize()
        {
            if (MainWindowConfig.Instance.OpenFloatingBall && DesktopPetConfig.Instance.ShowStartupGreeting)
                DesktopPetService.GetInstance().ShowStartupGreeting();

            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
