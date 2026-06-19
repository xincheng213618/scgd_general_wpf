using ColorVision.Themes;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public enum ServiceManagerSetupChoice
    {
        None,
        ImportConfiguration,
        ManualConfiguration,
        SkipConfiguration
    }

    public partial class ServiceManagerSetupChoiceWindow : Window
    {
        private readonly string _legacyConfigPath;

        public ServiceManagerSetupChoice SelectedChoice { get; private set; } = ServiceManagerSetupChoice.None;

        public ServiceManagerSetupChoiceWindow(string legacyConfigPath)
        {
            _legacyConfigPath = legacyConfigPath;
            InitializeComponent();
            this.ApplyCaption();
            UpdateLegacyConfigText();
        }

        private void UpdateLegacyConfigText()
        {
            LegacyConfigText.Text = File.Exists(_legacyConfigPath)
                ? "检测到旧版配置：" + _legacyConfigPath
                : "未检测到旧版配置。导入时可以选择旧版 CVWinSMS.exe，手动配置可直接填写 MySQL 和 MQTT。";
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            SelectedChoice = ServiceManagerSetupChoice.ImportConfiguration;
            DialogResult = true;
        }

        private void Manual_Click(object sender, RoutedEventArgs e)
        {
            SelectedChoice = ServiceManagerSetupChoice.ManualConfiguration;
            DialogResult = true;
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            SelectedChoice = ServiceManagerSetupChoice.SkipConfiguration;
            DialogResult = true;
        }
    }
}
