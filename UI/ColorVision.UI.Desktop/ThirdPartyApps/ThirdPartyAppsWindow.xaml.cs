using ColorVision.Common.ThirdPartyApps;
using ColorVision.Themes;
using ColorVision.UI;
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
        private static ThirdPartyAppsWindow? _instance;

        public static void ShowInstance()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new ThirdPartyAppsWindow();
                _instance.Owner = Application.Current.GetActiveWindow();
                _instance.Show();
            }
            else
            {
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;
                _instance.Activate();
            }
        }

        private ObservableCollection<ThirdPartyAppInfo> _allApps = new();
        private List<string> _groups = new();
        private const string AllGroupsKey = "All";
        private CustomAppsConfig _customConfig = null!;

        public ThirdPartyAppsWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _customConfig = CustomAppsConfig.Instance;
            var manager = ThirdPartyAppManager.GetInstance();
            _allApps = manager.Apps;
            RefreshGroups();
        }

        private void RefreshGroups()
        {
            _groups = _allApps
                .GroupBy(a => a.Group)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .OrderBy(g => g.Min(a => a.Order))
                .Select(g => g.Key)
                .ToList();

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

            var result = filtered.OrderBy(a => a.Order).ThenBy(a => a.Name).ToList();
            AppsListBox.ItemsSource = result;
            AppCountText.Text = result.Count.ToString();
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

                // Check if this is a custom app — if so, show edit and delete options
                var customEntry = FindCustomEntry(app);
                if (customEntry != null)
                {
                    contextMenu.Items.Add(new Separator());

                    MenuItem editItem = new MenuItem { Header = "编辑" };
                    editItem.Click += (s, args) => EditCustomApp(customEntry, app);
                    contextMenu.Items.Add(editItem);

                    MenuItem deleteItem = new MenuItem { Header = "删除" };
                    deleteItem.Click += (s, args) => DeleteCustomApp(customEntry, app);
                    contextMenu.Items.Add(deleteItem);
                }

                contextMenu.IsOpen = true;
            }
        }

        private CustomAppEntry? FindCustomEntry(ThirdPartyAppInfo app)
        {
            return _customConfig.Entries.FirstOrDefault(entry =>
                entry.Name == app.Name && MatchesCustomEntry(entry, app));
        }

        private static bool MatchesCustomEntry(CustomAppEntry entry, ThirdPartyAppInfo app)
        {
            switch (entry.AppType)
            {
                case CustomAppType.Executable:
                    return app.LaunchPath == entry.Command;
                case CustomAppType.CmdScript:
                    return app.LaunchPath == "cmd.exe";
                case CustomAppType.PowerShellScript:
                    return app.LaunchPath == "powershell.exe";
                default:
                    return false;
            }
        }

        private void EditCustomApp(CustomAppEntry entry, ThirdPartyAppInfo app)
        {
            var dlg = new AddCustomAppWindow(entry) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                entry.Name = dlg.Result.Name;
                entry.Command = dlg.Result.Command;
                entry.Arguments = dlg.Result.Arguments;
                entry.WorkingDirectory = dlg.Result.WorkingDirectory;
                entry.Group = dlg.Result.Group;
                entry.AppType = dlg.Result.AppType;

                ReloadAllApps();
            }
        }

        private void DeleteCustomApp(CustomAppEntry entry, ThirdPartyAppInfo app)
        {
            if (MessageBox.Show($"确定要删除自定义应用 \"{entry.Name}\" 吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _customConfig.Entries.Remove(entry);
                _allApps.Remove(app);
                RefreshGroups();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ReloadAllApps();
        }

        private void BtnAddApp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddCustomAppWindow { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _customConfig.Entries.Add(dlg.Result);
                ReloadAllApps();
            }
        }

        private void BtnAddScript_Click(object sender, RoutedEventArgs e)
        {
            var entry = new CustomAppEntry { AppType = CustomAppType.CmdScript };
            var dlg = new AddCustomAppWindow(entry) { Owner = this };
            dlg.Title = "添加快捷脚本";
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _customConfig.Entries.Add(dlg.Result);
                ReloadAllApps();
            }
        }

        private void ReloadAllApps()
        {
            ThirdPartyAppManager.GetInstance().LoadApps();
            _allApps = ThirdPartyAppManager.GetInstance().Apps;
            RefreshGroups();
        }
    }
}
