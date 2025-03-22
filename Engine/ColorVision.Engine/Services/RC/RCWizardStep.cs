using ColorVision.UI;
using System.Windows;


namespace ColorVision.Engine.Services.RC
{
    public class RCWizardStep : WizardStepBase
    {
        public override int Order => 3;

        public override string Header => "RC配置";
        public override string Description => "配置注册中心，如果已经正确配置服务可以点击服务配置即可不需要手动配置";

        public override void Execute()
        {
            new RCServiceConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

    }
}
