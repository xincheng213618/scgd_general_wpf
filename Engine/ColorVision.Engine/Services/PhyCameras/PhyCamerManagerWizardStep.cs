using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class PhyCamerManagerWizardStep : IWizardStep
    {
        public int Order => 20;

        public string Header => Properties.Resources.AddPhysicalCamera;
        public string Description => "用户可以在这里配置物理相机，也可以进入程序后手动配置物理相机，注意配置此项的前提时数据库，MQTT,注册中心都配置正确，且正常连接";

        public RelayCommand Command => new RelayCommand(a =>
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        });
    }
}
