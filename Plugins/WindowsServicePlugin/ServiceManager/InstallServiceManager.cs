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
        public override string ActionText => Properties.Resources.ServiceManager;
        public override bool ConfigurationStatus
        {
            get => IsServiceManagerConfigured();
            set { }
        }

        public override void Execute()
        {
            OpenServiceManagerWindow();
            OnPropertyChanged(nameof(ConfigurationStatus));
            OnPropertyChanged(nameof(Description));
        }

        private static string BuildDescription()
        {
            string description = "打开服务管理器，确认服务根目录、MySQL、MQTT 和 Windows 服务状态。关闭服务管理器后，向导会重新检查配置结果。";

            if (LegacyServiceConfig.TryGetAppConfigPath(out string legacyConfigPath))
            {
                description += Environment.NewLine + "旧版配置：" + legacyConfigPath;
            }

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

        private static void OpenServiceManagerWindow()
        {
            new ServiceManagerWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }
    }
}
