using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Diagnostics;

public sealed class MenuServiceProcessDiagnostics : MenuItemBase
{
    private static ServiceProcessWindow? window;

    public override string OwnerGuid => MenuItemConstants.Tool;
    public override string Header => "服务进程诊断";
    public override int Order => 21;
    public override ICommand? Command => ExperimentalFeaturesConfig.Instance.IsEnabled ? base.Command : null;

    public override void Execute()
    {
        if (!ExperimentalFeaturesConfig.Instance.IsEnabled) return;

        if (window is not null)
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new ServiceProcessWindow { Owner = Application.Current?.GetActiveWindow() };
        window.Closed += (_, _) => window = null;
        window.Show();
    }
}
