using ColorVision.Properties;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Update
{
    public class MenuChangeLog : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => Resources.ChangeLog;
        public override int Order => 1;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new ChangelogWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
