#pragma warning disable CA1863
using ColorVision.Common.ThirdPartyApps;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
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
        private CancellationTokenSource? _loadCancellation;
        private Authorization? _authorization;

        public ThirdPartyAppsWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            _customConfig = CustomAppsConfig.Instance;
            _allGroupsLabel = GetResourceString("ThirdPartyAppsAll", "All");
            _authorization = Authorization.Instance;
            if (_authorization != null)
                _authorization.PermissionModeChanged += Authorization_PermissionModeChanged;

            SearchBox.ToolTip = Properties.Resources.Search;
            BtnAddApp.ToolTip = Properties.Resources.CustomApp_AddTooltip;
            BtnAddScript.ToolTip = Properties.Resources.CustomApp_AddScriptTooltip;
            BtnRefresh.ToolTip = Properties.Resources.Refresh;
            GroupsLabelText.Text = Properties.Resources.CustomApp_Category;

            _allApps = ThirdPartyAppManager.GetInstance().Apps;
            await ReloadAllAppsAsync();
        }

        private void RefreshGroups()
        {
            var authorizedApps = _allApps.Where(app => app.IsAuthorized).ToList();
            _groups = authorizedApps
                .GroupBy(GetDisplayGroupKey)
                .OrderBy(group => group.Key.Category)
                .ThenBy(group => group.Min(app => app.Order))
                .ThenBy(group => group.Key.Group, StringComparer.CurrentCultureIgnoreCase)
                .Select(group => new ThirdPartyAppGroupItem(
                    GetDisplayGroupName(group.Key),
                    group.Count(),
                    group.Key))
                .ToList();

            GroupListBox.Items.Clear();
            GroupListBox.Items.Add(new ThirdPartyAppGroupItem(_allGroupsLabel, authorizedApps.Count, null, true));
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

            IEnumerable<ThirdPartyAppInfo> filtered = _allApps.Where(app => app.IsAuthorized);

            if (selectedGroup is { IsAll: false, GroupKey: not null })
            {
                filtered = filtered.Where(app => GetDisplayGroupKey(app) == selectedGroup.GroupKey.Value);
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                filtered = filtered.Where(app =>
                    (app.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (app.Group?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
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
                    editItem.Click += async (s, args) => await EditCustomAppAsync(customEntry, app);
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

        private async Task EditCustomAppAsync(CustomAppEntry entry, ThirdPartyAppInfo app)
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

                await ReloadAllAppsAsync(forceReload: true);
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

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await ReloadAllAppsAsync(forceReload: true, forceProviderRefresh: true);
        }

        private async void BtnAddApp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddCustomAppWindow { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _customConfig.Entries.Add(dlg.Result);
                await ReloadAllAppsAsync(forceReload: true);
            }
        }

        private async void BtnAddScript_Click(object sender, RoutedEventArgs e)
        {
            var entry = new CustomAppEntry { AppType = CustomAppType.CmdScript };
            var dlg = new AddCustomAppWindow(entry) { Owner = this };
            dlg.Title = Properties.Resources.CustomApp_AddScriptTitle;
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _customConfig.Entries.Add(dlg.Result);
                await ReloadAllAppsAsync(forceReload: true);
            }
        }

        private async Task ReloadAllAppsAsync(bool forceReload = false, bool forceProviderRefresh = false)
        {
            var manager = ThirdPartyAppManager.GetInstance();
            if (!forceReload && manager.IsLoaded)
            {
                _allApps = manager.Apps;
                RefreshGroups();
                ApplyFilter();
                return;
            }

            _loadCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _loadCancellation = cancellation;

            SetLoadingState(true);

            try
            {
                await manager.LoadAppsAsync(
                    forceReload: forceReload,
                    forceProviderRefresh: forceProviderRefresh,
                    cancellationToken: cancellation.Token);
                if (cancellation.IsCancellationRequested)
                    return;

                _allApps = manager.Apps;
                RefreshGroups();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (_loadCancellation == cancellation)
                {
                    _loadCancellation = null;
                    SetLoadingState(false);
                    ApplyFilter();
                }

                cancellation.Dispose();
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            BtnRefresh.IsEnabled = !isLoading;
            BtnAddApp.IsEnabled = !isLoading;
            BtnAddScript.IsEnabled = !isLoading;
            SearchBox.IsEnabled = !isLoading;

            if (isLoading)
            {
                AppCountText.Text = $"{Properties.Resources.Loading}...";
                CurrentGroupText.Text = Properties.Resources.Loading;
                AppsListBox.ItemsSource = null;
                GroupListBox.Items.Clear();
            }
        }

        private static string GetCategoryDisplayName(ThirdPartyAppCategory category)
        {
            bool isChinese = IsChineseUICulture();
            return category switch
            {
                ThirdPartyAppCategory.Internal => GetResourceString("ThirdPartyAppsCategoryInternal", isChinese ? "内部工具" : "Internal Tools"),
                ThirdPartyAppCategory.System => Properties.Resources.SystemTools,
                ThirdPartyAppCategory.External => GetResourceString("ThirdPartyAppsCategoryExternal", isChinese ? "外部应用" : "External Apps"),
                ThirdPartyAppCategory.Custom => GetResourceString("ThirdPartyAppsCategoryCustom", isChinese ? "自定义" : "Custom"),
                _ => category.ToString(),
            };
        }

        private static ThirdPartyAppGroupKey GetDisplayGroupKey(ThirdPartyAppInfo app)
        {
            string group = app.Category is ThirdPartyAppCategory.External or ThirdPartyAppCategory.Custom
                ? app.Group?.Trim() ?? string.Empty
                : string.Empty;
            return new ThirdPartyAppGroupKey(app.Category, group);
        }

        private static string GetDisplayGroupName(ThirdPartyAppGroupKey key)
        {
            string category = GetCategoryDisplayName(key.Category);
            return string.IsNullOrWhiteSpace(key.Group) ? category : $"{category} · {key.Group}";
        }

        private static bool IsChineseUICulture()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_authorization != null)
                _authorization.PermissionModeChanged -= Authorization_PermissionModeChanged;
            _authorization = null;
            _loadCancellation?.Cancel();
            _loadCancellation = null;
            base.OnClosed(e);
        }

        private void Authorization_PermissionModeChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded)
                    return;

                RefreshGroups();
                ApplyFilter();
            });
        }

        private static string GetResourceString(string key, string fallback)
        {
            return Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }

        internal readonly record struct ThirdPartyAppGroupKey(ThirdPartyAppCategory Category, string Group);

        public sealed class ThirdPartyAppGroupItem
        {
            internal ThirdPartyAppGroupItem(string name, int count, ThirdPartyAppGroupKey? groupKey, bool isAll = false)
            {
                Name = name;
                Count = count;
                GroupKey = groupKey;
                IsAll = isAll;
            }

            public string Name { get; }
            public string DisplayName => Name;
            public int Count { get; }
            internal ThirdPartyAppGroupKey? GroupKey { get; }
            public bool IsAll { get; }
        }
    }
}
