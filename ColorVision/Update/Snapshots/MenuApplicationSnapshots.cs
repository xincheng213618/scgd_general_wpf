using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Update
{
    public class MenuApplicationSnapshots : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => "程序备份";
        public override int Order => 2;

        public override void Execute()
        {
            new ApplicationSnapshotsWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }
    }
}
