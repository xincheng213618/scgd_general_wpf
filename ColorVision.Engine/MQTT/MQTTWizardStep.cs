using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MQTTWizardStep : WizardStepBase
    {
        public override int Order => 999;

        public override string Header => "Mqtt配置";

        public override void Execute()
        {
            MQTTConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow() };
            mySqlConnect.ShowDialog();
        }
    }
}
