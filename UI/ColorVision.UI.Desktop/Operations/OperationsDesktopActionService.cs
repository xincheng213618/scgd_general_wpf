using System.Windows;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed record OperationsActionResult(bool Success, string ActionId, string Message);

    public sealed record OperationsDesktopState(bool DispatcherAvailable, bool Exists, bool IsVisible, string WindowState);

    public static class OperationsDesktopActionService
    {
        public const string ShowWindowAction = "ops.window.show";
        public const string MinimizeWindowAction = "ops.window.minimize";

        public static OperationsActionResult Execute(string actionId)
        {
            return actionId switch
            {
                ShowWindowAction => ExecuteOnUiThread(actionId, ShowMainWindow),
                MinimizeWindowAction => ExecuteOnUiThread(actionId, MinimizeMainWindow),
                _ => new OperationsActionResult(false, actionId, "不支持的桌面操作。"),
            };
        }

        public static OperationsDesktopState CaptureState()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return new OperationsDesktopState(false, false, false, "Unavailable");

            try
            {
                return dispatcher.CheckAccess() ? CaptureStateOnUiThread() : dispatcher.Invoke(CaptureStateOnUiThread);
            }
            catch
            {
                return new OperationsDesktopState(true, false, false, "Unavailable");
            }
        }

        private static OperationsActionResult ExecuteOnUiThread(string actionId, Func<string> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return new OperationsActionResult(false, actionId, "当前没有可用的 WPF 调度器。");

            try
            {
                string message = dispatcher.CheckAccess() ? action() : dispatcher.Invoke(action);
                return new OperationsActionResult(true, actionId, message);
            }
            catch (Exception ex)
            {
                return new OperationsActionResult(false, actionId, ex.Message);
            }
        }

        private static string ShowMainWindow()
        {
            Window? window = Application.Current?.MainWindow;
            if (window == null)
                throw new InvalidOperationException("当前没有主窗口。");

            if (!window.IsVisible)
                window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            return "主窗口已显示。";
        }

        private static string MinimizeMainWindow()
        {
            Window? window = Application.Current?.MainWindow;
            if (window == null)
                throw new InvalidOperationException("当前没有主窗口。");

            window.WindowState = WindowState.Minimized;
            return "主窗口已最小化。";
        }

        private static OperationsDesktopState CaptureStateOnUiThread()
        {
            Window? window = Application.Current?.MainWindow;
            return window == null
                ? new OperationsDesktopState(true, false, false, "Missing")
                : new OperationsDesktopState(true, true, window.IsVisible, window.WindowState.ToString());
        }
    }
}
