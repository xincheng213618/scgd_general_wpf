using ColorVision.Themes;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public partial class ThirdPartyAppsWindow : Window
    {
        private ObservableCollection<ThirdPartyAppInfo> _allApps = new();

        public ThirdPartyAppsWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var manager = ThirdPartyAppManager.GetInstance();
            _allApps = manager.Apps;
            AppsItemsControl.ItemsSource = _allApps;
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string keyword = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                AppsItemsControl.ItemsSource = _allApps;
            }
            else
            {
                var filtered = _allApps.Where(a => a.Name != null && a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
                AppsItemsControl.ItemsSource = filtered;
            }
        }

        private void AppItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is ThirdPartyAppInfo app)
            {
                app.DoubleClickCommand.Execute(null);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ThirdPartyAppManager.GetInstance().Refresh();
            SearchBox_TextChanged(sender, new System.Windows.Controls.TextChangedEventArgs(System.Windows.Controls.TextBox.TextChangedEvent, System.Windows.Controls.UndoAction.None));
        }
    }
}
