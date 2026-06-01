using ColorVision.UI;

namespace WindowsServicePlugin.ServiceManager
{
    public class InstallServiceManager : WizardStepBase
    {
        public override int Order => 0;
        public override string Header => Properties.Resources.ServiceManager;
        public override string Description => Properties.Resources.ServiceManager;
        public override void Execute()
        {
            var window = new ServiceManagerWindow();
            window.Show();
        }
    }
}
