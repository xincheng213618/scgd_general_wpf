using ColorVision.Themes;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.Settings
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow
    {
        private SettingWindowController? _controller;

        public SettingWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _controller = new SettingWindowController(SearchTextBox, NavigationListBox, SettingsContentPanel, CurrentGroupTitle, CurrentGroupDescription);
            _controller.LoadConfigSettings();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _controller?.RefreshNavigationAndContent();
        }

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationListBox.SelectedItem is NavigationEntry navigationEntry)
            {
                _controller?.SelectGroup(navigationEntry.Group);
            }
        }
    }
}