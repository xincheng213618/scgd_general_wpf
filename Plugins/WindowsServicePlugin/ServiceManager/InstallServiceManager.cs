using ColorVision.UI;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public class InstallServiceManager : WizardStepBase
    {
        public override int Order => 0;
        public override string Header => Properties.Resources.ServiceManager;
        public override string Description => BuildDescription();
        public override string ActionText => Properties.Resources.Install;
        public override bool ConfigurationStatus
        {
            get => IsServiceManagerConfigured();
            set { }
        }

        public override void Execute()
        {
            OpenServiceInstallationWindow();
            OnPropertyChanged(nameof(ConfigurationStatus));
            OnPropertyChanged(nameof(Description));
        }

        private static string BuildDescription()
        {
            string description = "安装 Windows 服务，配置安装目录、MySQL 和 MQTT。关闭安装窗口后，向导会重新检查配置结果。";

            string baseLocation = ServiceManagerConfig.Instance.BaseLocation;
            if (!string.IsNullOrWhiteSpace(baseLocation))
            {
                description += Environment.NewLine + "当前服务根目录：" + baseLocation;
            }

            return description;
        }

        internal static bool IsServiceManagerConfigured()
        {
            string baseLocation = ServiceManagerConfig.Instance.BaseLocation;
            return !string.IsNullOrWhiteSpace(baseLocation) && Directory.Exists(baseLocation);
        }

        private static void OpenServiceInstallationWindow()
        {
            new ServiceInstallWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }
    }
}
