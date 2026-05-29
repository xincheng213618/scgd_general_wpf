using ColorVision.Themes;
using System;
using System.Linq;
using System.Windows;

namespace ColorVision.UI.Serach
{
    public partial class SearchSettingsWindow : Window
    {
        public SearchSettingsWindow()
        {
            InitializeComponent();
            DataContext = SearchConfig.Instance;
            SearchEngineComboBox.ItemsSource = Enum.GetValues<SearchEngine>().Cast<SearchEngine>().ToList();
            ApplyLocalization();
            this.ApplyCaption();
        }

        private void ApplyLocalization()
        {
            Title = GetText("SearchSettingsTitle");
            CloseButton.Content = Properties.Resources.Close;
            HeaderText.Text = GetText("SearchSettingsTitle");
            HeaderHintText.Text = GetText("SearchSettingsHint");

            IndexedSourcesTitleText.Text = GetText("SearchIndexedSourcesTitle");
            IndexedSourcesDescriptionText.Text = GetText("SearchIndexedSourcesDescription");
            MenuCommandsCheckBox.Content = GetText("SearchMenuCommands");
            MenuCommandsHintText.Text = GetText("SearchMenuCommandsDescription");
            TemplatesCheckBox.Content = GetText("SearchTemplates");
            TemplatesHintText.Text = GetText("SearchTemplatesDescription");
            ThirdPartyAppsCheckBox.Content = GetText("SearchThirdPartyApps");
            ThirdPartyAppsHintText.Text = GetText("SearchThirdPartyAppsDescription");

            ExternalActionsTitleText.Text = GetText("SearchExternalActionsTitle");
            ExternalActionsDescriptionText.Text = GetText("SearchExternalActionsDescription");
            EverythingCheckBox.Content = Properties.Resources.EnableEverythingSearch;
            EverythingHintText.Text = GetText("SearchEverythingDescription");
            EverythingPathLabelText.Text = Properties.Resources.EverythingPath;
            BrowserSearchCheckBox.Content = Properties.Resources.EnableBrowserSearch;
            BrowserHintText.Text = GetText("SearchBrowserDescription");
            BrowserEngineLabelText.Text = GetText("SearchBrowserEngine");
        }

        private static string GetText(string key)
        {
            return Properties.Resources.ResourceManager.GetString(key, Properties.Resources.Culture) ?? key;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}