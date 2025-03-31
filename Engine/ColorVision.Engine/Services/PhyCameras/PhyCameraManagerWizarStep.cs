using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class PhyCameraManagerWizarStep : WizardStepBase
    {
        public override int Order => 6;

        public override string Header => "配置物理相机";
        public override string Description => "配置物理相机之前，需要先配置数据库以及MQTT和RC";

        public override void Execute()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
            ConfigurationStatus = PhyCameraManager.GetInstance().PhyCameras.Count > 0;
        }
        public override bool ConfigurationStatus { get => PhyCameraManager.GetInstance().PhyCameras.Count >0; set { _ConfigurationStatus = value; NotifyPropertyChanged(); } }
        private bool _ConfigurationStatus = true;

    }
}
