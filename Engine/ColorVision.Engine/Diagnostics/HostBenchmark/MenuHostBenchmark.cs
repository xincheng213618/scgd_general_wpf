using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

public sealed class MenuHostBenchmark : MenuItemBase
{
    private static HostBenchmarkWindow? window;

    public override string OwnerGuid => MenuItemConstants.Tool;
    public override string GuidId => nameof(MenuHostBenchmark);
    public override string Header => "基准测试";
    public override int Order => 20;

    public override void Execute()
    {
        if (window is not null)
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new HostBenchmarkWindow { Owner = Application.Current.GetActiveWindow() };
        window.Closed += (_, _) => window = null;
        window.Show();
    }
}
