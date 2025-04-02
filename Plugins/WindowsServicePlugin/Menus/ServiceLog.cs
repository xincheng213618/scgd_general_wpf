using ColorVision.UI.Menus;

namespace WindowsServicePlugin.Menus
{
    public class ServiceLog : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "ServiceLog";
        public override string Header => WindowsServicePlugin.Properties.Resources.Service;
    }
}
