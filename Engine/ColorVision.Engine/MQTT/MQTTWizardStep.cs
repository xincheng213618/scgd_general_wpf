using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MQTTWizardStep : WizardStepBase
    {
        public override int Order => 10;

        public override string Header => "Mqtt配置";

        public override string Description => "配置Mqtt连接，默认为本地连接，可以不进行配置";

        public override void Execute()
        {
            MQTTConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow() };
            mySqlConnect.ShowDialog();
        }
    }
}
