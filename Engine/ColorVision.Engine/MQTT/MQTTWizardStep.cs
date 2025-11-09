using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MQTTWizardStep : WizardStepBase
    {
        public override int Order => 10;

        public override string Header => ColorVision.Engine.Properties.Resources.MqttConfig;

        public override string Description => ColorVision.Engine.Properties.Resources.ConfigureMqttConnection_DefaultsToLocalOptional;

        public override void Execute()
        {
            MQTTConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow() };
            mySqlConnect.ShowDialog();
        }
    }
}
