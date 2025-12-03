using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using System.Windows;

namespace ImageProjector
{
    public class MenuImageProjector : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Properties.Resources.ImageProjector;
        public override int Order => 3;

        public override void Execute()
        {
            new ImageProjectorWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
