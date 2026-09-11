using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Guidance;

public sealed class MenuNewUserGuide : MenuItemBase
{
    public override string OwnerGuid => MenuItemConstants.Help;
    public override int Order => 100;
    public override string Header => NewUserGuideText.Get("MenuHeader");

    public override void Execute()
    {
        if (Application.Current?.MainWindow is MainWindow mainWindow)
            mainWindow.ShowNewUserGuide();
    }
}
