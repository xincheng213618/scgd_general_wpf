using ColorVision.UI;

namespace WindowsServicePlugin.ServiceManager
{
    public class InstallServiceManager : WizardStepBase
    {
        public override int Order => 0;
        public override string Header => "服务管理器";
        public override string Description => "服务管理器";
        public override void Execute()
        {
            var window = new ServiceManagerWindow();
            window.Show();
        }
    }
}
