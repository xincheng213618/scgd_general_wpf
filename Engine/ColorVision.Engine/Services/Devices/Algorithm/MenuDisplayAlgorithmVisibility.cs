using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class MenuDisplayAlgorithmVisibility : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Properties.Resources.AlgorithmVisibilitySettings;
        public override int Order => 10;

        public override void Execute()
        {
            new DisplayAlgorithmVisibilityWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
