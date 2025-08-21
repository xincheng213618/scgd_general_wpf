using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace Spectrum.Update
{
    public class MenuChangeLog : MenuItemBase
    {
        public override string OwnerGuid =>MenuItemConstants.Help;
        public override string Header => "ChangeLog";
        public override int Order => 1;

        public override void Execute()
        {
            new ChangelogWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }


}
