using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace EventVWR
{

    public class ExportEventWindow : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "EventWindow";
        public override int Order => 1000;
        public override string Header => "事件查看器";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new EventWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
