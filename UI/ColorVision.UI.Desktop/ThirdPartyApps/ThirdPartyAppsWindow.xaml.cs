using ColorVision.Common.ThirdPartyApps;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    public partial class ThirdPartyAppsWindow : Window
    {
        private ObservableCollection<ThirdPartyAppInfo> _allApps = new();
        private List<string> _groups = new();
        private const string AllGroupsKey = "All";

        public ThirdPartyAppsWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var manager = ThirdPartyAppManager.GetInstance();
            _allApps = manager.Apps;
            RefreshGroups();
        }

        private void RefreshGroups()
        {
            _groups = _allApps.Select(a => a.Group).Where(g => !string.IsNullOrEmpty(g)).Distinct().ToList();

            GroupListBox.Items.Clear();
            GroupListBox.Items.Add(AllGroupsKey);
            foreach (var group in _groups)
            {
                GroupListBox.Items.Add(group);
            }
            GroupListBox.SelectedIndex = 0;
        }

        private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string keyword = SearchBox.Text.Trim();
            string? selectedGroup = GroupListBox.SelectedItem as string;

            IEnumerable<ThirdPartyAppInfo> filtered = _allApps;

            if (!string.IsNullOrEmpty(selectedGroup) && selectedGroup != AllGroupsKey)
            {
                filtered = filtered.Where(a => a.Group == selectedGroup);
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                filtered = filtered.Where(a => a.Name != null && a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            AppsListBox.ItemsSource = filtered.ToList();
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
            RefreshGroups();
        }
    }
}
