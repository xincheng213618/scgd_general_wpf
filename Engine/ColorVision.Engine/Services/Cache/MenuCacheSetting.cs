using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services
{
    public class MenuCacheSetting : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => ColorVision.Engine.Properties.Resources.CacheManagement;
        public override int Order => 3;

        public override void Execute()
        {
            new CacheSettingWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
