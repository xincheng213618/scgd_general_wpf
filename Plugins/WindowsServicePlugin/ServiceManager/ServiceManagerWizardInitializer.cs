using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public class ServiceManagerWizardInitializer : IWizardInitializer
    {
        public int Order => 0;

        public void Initialize(WizardInitializationContext context)
        {
            if (!context.IsFirstRun)
            {
                return;
            }

            LegacyServiceConfig.TryGetAppConfigPath(out string legacyConfigPath);
            ServiceManagerSetupChoiceWindow window = new(legacyConfigPath)
            {
                Owner = context.Owner
            };

            if (window.ShowDialog() != true)
            {
                return;
            }

            switch (window.SelectedChoice)
            {
                case ServiceManagerSetupChoice.ImportConfiguration:
                    ImportLegacyConfiguration(context.Owner);
                    break;
                case ServiceManagerSetupChoice.ManualConfiguration:
                    OpenManualConfiguration(context.Owner);
                    break;
                case ServiceManagerSetupChoice.SkipConfiguration:
                    context.RequestSkipWizard();
                    break;
            }
        }

        private static void ImportLegacyConfiguration(Window owner)
        {
            if (!LegacyServiceConfig.EnsureAppConfigPath(owner, out string legacyConfigPath))
            {
                MessageBox.Show(
                    owner,
                    "没有找到旧版 CVWinSMS.exe 或它旁边的 config\\App.config。",
                    "服务管理器配置",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            bool imported = LegacyServiceConfig.Import(legacyConfigPath, out string message);
            MessageBox.Show(
                owner,
                message,
                "服务管理器配置",
                MessageBoxButton.OK,
                imported ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private static void OpenManualConfiguration(Window owner)
        {
            MySqlConnect mySqlConnect = new()
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            mySqlConnect.ShowDialog();

            MQTTConnect mqttConnect = new()
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            mqttConnect.ShowDialog();

            ConfigHandler configHandler = ConfigHandler.GetInstance();
            configHandler.Save<MySqlSetting>();
            configHandler.Save<MQTTSetting>();
        }
    }
}
