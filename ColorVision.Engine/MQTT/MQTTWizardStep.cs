using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MQTTWizardStep : IWizardStep
    {
        public int Order => 2;

        public string Title => "Mqtt配置";

        public string Description => "Mqtt配置";

        public RelayCommand? RelayCommand => new RelayCommand(a =>
        {
            MQTTConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow()};
            mySqlConnect.ShowDialog();
        });
    }
}
