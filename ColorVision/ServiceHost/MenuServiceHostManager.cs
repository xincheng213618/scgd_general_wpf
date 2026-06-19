using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.Update;
using System.Windows;

namespace ColorVision.ServiceHost
{
    public sealed class MenuServiceHostManager : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);

        public override string Header => "ColorVision Service Host";

        public override int Order => 1003;

        public override void Execute()
        {
            ServiceHostManagerWindow window = new()
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.ShowDialog();
        }
    }
}
