using ColorVision.Solution.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Solution.Fusion
{
    public class MenuFusion : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 100;
        public override string Header => Resources.MenuFusion;

        public override void Execute()
        {
            var window = new FusionWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }
    }
}
