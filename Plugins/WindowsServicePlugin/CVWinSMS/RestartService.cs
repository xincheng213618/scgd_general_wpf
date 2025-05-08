#pragma warning disable SYSLIB0014
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;


namespace WindowsServicePlugin.CVWinSMS
{
    public class RestartService : MenuItemBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string Header => "重启服务";
        public override int Order => 2;

        public override void Execute()
        {
            if (Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService&&net start RegistrationCenterService"))
            {
                MessageBox.Show("重启服务成功");
            }
            else
            {
                MessageBox.Show("重启服务失败请手动重启服务");
            }
        }
    }
}
