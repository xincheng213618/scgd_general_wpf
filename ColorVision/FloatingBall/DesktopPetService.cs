#pragma warning disable CA1822
using ColorVision.UI;
using System;
using System.Windows;

namespace ColorVision.FloatingBall
{
    public class DesktopPetService
    {
        private static readonly Lazy<DesktopPetService> LazyInstance = new(() => new DesktopPetService());
        private FloatingBallWindow? _window;

        public static DesktopPetService GetInstance() => LazyInstance.Value;

        public bool IsVisible => _window != null && _window.IsVisible;

        public FloatingBallWindow Show()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
                return Application.Current.Dispatcher.Invoke(Show);

            if (_window == null)
            {
                _window = new FloatingBallWindow();
            }

            _window.Show();
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

            var window = new PropertyEditorWindow(DesktopPetConfig.Instance)
            {
                Title = Properties.Resources.DesktopPetSettingsTitle,
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Submited += (_, _) =>
            {
                ConfigHandler.GetInstance().Save<DesktopPetConfig>();
            };
            window.Show();
        }
    }

    public class DesktopPetInitializer : MainWindowInitializedBase
    {
        public override string Name => Properties.Resources.DesktopPetInitializerName;
        public override int Order { get; set; } = 1000;

        public override async System.Threading.Tasks.Task Initialize()
        {
            await System.Threading.Tasks.Task.Delay(600);
            DesktopPetService.GetInstance().ShowStartupGreeting();
        }
    }
}
