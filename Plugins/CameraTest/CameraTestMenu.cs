using ColorVision.UI.Extension;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace CameraTest;

public sealed class CameraTestMenu : MenuItemBase
{
    public override string OwnerGuid => MenuItemConstants.Tool;
    public override string Header => "相机生产调试";
    public override int Order => 4;
    public override void Execute() => new CameraTestWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen }.Show();
}

public sealed class CameraTestLauncher : IFeatureLauncherBase
{
    public override string? Header { get; set; } = "相机生产调试";
    public override void Execute() => new CameraTestWindow().Show();
}
