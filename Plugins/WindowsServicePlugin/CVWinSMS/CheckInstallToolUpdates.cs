using ColorVision.UI.Menus;

namespace WindowsServicePlugin.CVWinSMS
{
    public sealed class CheckInstallToolUpdates : MenuItemBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "CheckInstallToolUpdates";
        public override int Order => 2;
        public override string Header => "检查旧服务管理工具更新";

        public override async void Execute()
        {
            await InstallTool.ExecuteMenuActionAsync(
                () => new InstallTool().GetLatestReleaseVersion(),
                InstallTool.ReportUpdateCheckFailure);
        }
    }
}
