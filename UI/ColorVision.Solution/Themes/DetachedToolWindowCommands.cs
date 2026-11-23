using ColorVision.Common.MVVM;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution.Themes;

public static class DetachedToolWindowCommands
{
    public static ICommand ToggleMaximize { get; } = new RelayCommand<Window>(window =>
    {
        if (window == null)
            return;

        if (window.WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(window);
        else
            SystemCommands.MaximizeWindow(window);
    }, window => window != null);

    public static ICommand Close { get; } = new RelayCommand<Window>(window => window?.Close(), window => window != null);
}
