using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;


namespace WindowsServicePlugin.CVWinSMS
{
    public class RestartService : MenuItemBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string Header => WindowsServicePlugin.Properties.Resources.RestartService;
        public override int Order => 2;

        public override void Execute()
        {
            if (Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService&&net start RegistrationCenterService"))
            {
                MessageBox.Show(WindowsServicePlugin.Properties.Resources.RestartServiceSucess);
            }
            else
            {
                MessageBox.Show(WindowsServicePlugin.Properties.Resources.ServiceRestartFailed_PleaseRestartManually);
            }
        }
    }
}
