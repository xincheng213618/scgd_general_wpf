using ColorVision.Themes;
using System.IO;
using System.Windows;
using PluginResources = WindowsServicePlugin.Properties.Resources;

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
                ? string.Format(PluginResources.LegacyConfigDetectedFormat, _legacyConfigPath)
                : PluginResources.LegacyConfigNotDetected;
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
