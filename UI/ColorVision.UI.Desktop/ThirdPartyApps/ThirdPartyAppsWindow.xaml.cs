#pragma warning disable CA1863
using ColorVision.Common.ThirdPartyApps;
using ColorVision.Themes;
using System.Collections.ObjectModel;
using System.Globalization;
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
        private List<ThirdPartyAppGroupItem> _groups = new();
        private string _allGroupsLabel = string.Empty;
        private CustomAppsConfig _customConfig = null!;

        public ThirdPartyAppsWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _customConfig = CustomAppsConfig.Instance;
            _allGroupsLabel = GetResourceString("ThirdPartyAppsAll", "All");

            SearchBox.ToolTip = Properties.Resources.Search;
            BtnAddApp.ToolTip = Properties.Resources.CustomApp_AddTooltip;
            BtnAddScript.ToolTip = Properties.Resources.CustomApp_AddScriptTooltip;
            BtnRefresh.ToolTip = Properties.Resources.Refresh;
            GroupsLabelText.Text = Properties.Resources.CustomApp_Category;

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
                .Select(g => new ThirdPartyAppGroupItem(g.Key, g.Count()))
                .ToList();

            GroupListBox.Items.Clear();
            GroupListBox.Items.Add(new ThirdPartyAppGroupItem(_allGroupsLabel, _allApps.Count, true));
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
            ThirdPartyAppGroupItem? selectedGroup = GroupListBox.SelectedItem as ThirdPartyAppGroupItem;

            IEnumerable<ThirdPartyAppInfo> filtered = _allApps;

            if (selectedGroup is { IsAll: false })
            {
                filtered = filtered.Where(a => a.Group == selectedGroup.Name);
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                filtered = filtered.Where(a => a.Name != null && a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            var result = filtered.OrderBy(a => a.Order).ThenBy(a => a.Name).ToList();
            AppsListBox.ItemsSource = result;
            AppCountText.Text = string.Format(
                CultureInfo.CurrentUICulture,
                GetResourceString("ThirdPartyAppsCountFormat", "{0} apps"),
                result.Count);
            CurrentGroupText.Text = selectedGroup?.DisplayName ?? _allGroupsLabel;
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
            if (e.OriginalSource is DependencyObject source)
            {
                if (ItemsControl.ContainerFromElement(AppsListBox, source) is ListBoxItem listBoxItem)
                    listBoxItem.IsSelected = true;
            }

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

                if (app.ContextActions.Count > 0)
                {
                    contextMenu.Items.Add(new Separator());

                    foreach (var action in app.ContextActions)
                    {
                        MenuItem actionItem = new MenuItem { Header = action.Header, IsEnabled = action.IsEnabled };
                        actionItem.Click += (s, args) =>
                        {
                            try
                            {
                                action.Invoke();
                                app.RefreshStatus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        };
                        contextMenu.Items.Add(actionItem);
                    }
                }

                var customEntry = FindCustomEntry(app);
                if (customEntry != null)
                {
                    contextMenu.Items.Add(new Separator());

                    MenuItem editItem = new MenuItem { Header = Properties.Resources.Edit };
                    editItem.Click += (s, args) => EditCustomApp(customEntry, app);
                    contextMenu.Items.Add(editItem);

                    MenuItem deleteItem = new MenuItem { Header = Properties.Resources.Delete };
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
            string message = string.Format(
                CultureInfo.CurrentUICulture,
                Properties.Resources.CustomApp_ConfirmDelete,
                entry.Name);

            if (MessageBox.Show(message, Properties.Resources.CustomApp_ConfirmDeleteTitle,
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
            dlg.Title = Properties.Resources.CustomApp_AddScriptTitle;
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

        private static string GetResourceString(string key, string fallback)
        {
            return Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }

        public sealed class ThirdPartyAppGroupItem
        {
            public ThirdPartyAppGroupItem(string name, int count, bool isAll = false)
            {
                Name = name;
                Count = count;
                IsAll = isAll;
            }

            public string Name { get; }
            public string DisplayName => Name;
            public int Count { get; }
            public bool IsAll { get; }
        }
    }
}
