using ColorVision.UI.Menus;

namespace WindowsServicePlugin.ServiceManager
{
    public class MenuServiceManager : MenuItemBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "ServiceManager";
        public override int Order => 0;
        public override string Header => "服务管理器";

        public override void Execute()
        {
            var window = new ServiceManagerWindow();
            window.Show();
        }
    }
}
