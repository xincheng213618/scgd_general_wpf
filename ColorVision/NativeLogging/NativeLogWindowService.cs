using System.Linq;
using System.Windows;

namespace ColorVision.NativeLogging;

internal static class NativeLogWindowService
{
    public static void Show()
    {
        Application? application = Application.Current;
        if (application == null)
        {
            return;
        }

        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.BeginInvoke(Show);
            return;
        }

        NativeLogWindow? existingWindow = application.Windows
            .OfType<NativeLogWindow>()
            .FirstOrDefault();
        if (existingWindow != null)
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            return;
        }

        Window? owner = application.GetActiveWindow();
        NativeLogWindow window = new()
        {
            Owner = owner,
            WindowStartupLocation = owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
        };
        window.Show();
        window.Activate();
    }
}
