using ColorVision.Themes;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            AppsListBox.ItemsSource = _allApps;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string keyword = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                AppsListBox.ItemsSource = _allApps;
            }
            else
            {
                var filtered = _allApps.Where(a => a.Name != null && a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
                AppsListBox.ItemsSource = filtered;
            }
        }

        private void AppsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AppsListBox.SelectedItem is ThirdPartyAppInfo app)
            {
                app.DoubleClickCommand.Execute(null);
            }
        }

        private void AppsListBox_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (AppsListBox.SelectedItem is ThirdPartyAppInfo app)
            {
                ContextMenu contextMenu = new ContextMenu();
                contextMenu.PlacementTarget = AppsListBox;

                MenuItem openItem = new MenuItem { Header = Properties.Resources.Open };
                openItem.Click += (s, args) => app.DoubleClickCommand.Execute(null);
                contextMenu.Items.Add(openItem);

                MenuItem openDirItem = new MenuItem { Header = Properties.Resources.OpenDirectory };
                openDirItem.Click += (s, args) => app.OpenDirectoryCommand.Execute(null);
                openDirItem.IsEnabled = app.OpenDirectoryCommand.CanExecute(null);
                contextMenu.Items.Add(openDirItem);

                contextMenu.IsOpen = true;
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ThirdPartyAppManager.GetInstance().Refresh();
            ApplyFilter();
        }
    }
}
