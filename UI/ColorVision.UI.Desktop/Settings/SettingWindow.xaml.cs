using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Desktop.Settings
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow
    {
        private SettingWindowController? _controller;
        private readonly IEnumerable<ConfigSettingMetadata>? _settingsOverride;

        public SettingWindow() : this(null) { }

        internal SettingWindow(IEnumerable<ConfigSettingMetadata>? settings)
        {
            _settingsOverride = settings;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _controller = new SettingWindowController(SearchTextBox, NavigationListBox, SettingsContentPanel, CurrentGroupTitle, CurrentGroupDescription);
            _controller.LoadConfigSettings(_settingsOverride);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _controller?.RefreshNavigationAndContent();
            SettingsScrollViewer?.ScrollToTop();
        }

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationListBox.SelectedItem is NavigationEntry navigationEntry)
            {
                _controller?.SelectGroup(navigationEntry.Group);
                SettingsScrollViewer.ScrollToTop();
            }
        }

        public bool NavigateToSetting(string settingId)
        {
            FrameworkElement? target = _controller?.NavigateToSetting(settingId);
            if (target == null) return false;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (IsVisible && target.IsVisible) target.BringIntoView();
            }));
            return true;
        }
    }
}
