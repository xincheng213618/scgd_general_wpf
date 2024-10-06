using ColorVision.UI.Menus;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{
    public class ExportServiceLog : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "ServiceLog";
        public override string Header => Resources.ServiceLog;
    }
}
