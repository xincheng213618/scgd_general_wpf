using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
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
                    Category = ThirdPartyAppCategory.Internal,
                    RequiredPermission = PermissionMode.Administrator,
                    ActionId = "maintenance.services.open",
                    ResourceId = "windows-services",
                    Order = 3,
                    IconGlyph = ThirdPartyAppIconGlyphs.ServiceManager,
                    LaunchAction = OpenServiceManagerWindow,
                }
            };
        }

        internal static void OpenServiceManagerWindow()
        {
            AccessControl.Demand("maintenance.services.open", PermissionMode.Administrator, "windows-services");
            new ServiceManagerWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.Show();
        }
    }

    public class MenuServiceManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string GuidId => "ServiceManager";
        public override int Order => 0;
        public override string Header => LocalizedMenuAccessKey.Format(WindowsServicePlugin.Properties.Resources.ServiceManager, 'M');

        [RequiresPermission(PermissionMode.Administrator, ActionId = "maintenance.services.open", ResourceId = "windows-services")]
        public override void Execute() => ServiceManagerAppProvider.OpenServiceManagerWindow();
    }
}
