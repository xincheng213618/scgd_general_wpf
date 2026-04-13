using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public class ServiceManagerAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "服务管理器",
                    Group = "内部工具",
                    Order = 3,
                    LaunchAction = () =>
                    {
                        new ServiceManagerWindow
                        {
                            Owner = Application.Current.GetActiveWindow(),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        }.Show();
                    },
                }
            };
        }
    }

    public class MenuServiceManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
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
