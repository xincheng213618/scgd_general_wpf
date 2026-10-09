using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Caches;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    public partial class LocalCalibrationCacheManagerWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalCalibrationCacheManagerWindow));
        private static LocalCalibrationCacheManagerWindow? instance;
        private readonly ObservableCollection<CacheModuleViewItem> modules = new();
        private readonly ObservableCollection<CacheNavigationItem> navigation = new();
        private readonly CvRawFileCacheConfig imageCacheConfig;
        private readonly CameraRawBufferCacheConfig cameraCacheConfig;
        private readonly ICollectionView modulesView;
        private bool isBusy;
        private bool isClosed;
        private bool isApplyingSnapshot;
        private long? currentSnapshotSourceId;
        private string? detailsModuleId;

        public LocalCalibrationCacheManagerWindow()
        {
            imageCacheConfig = CvRawFileCacheConfig.Current;
            cameraCacheConfig = CameraRawBufferCacheConfig.Current;
            modulesView = CollectionViewSource.GetDefaultView(modules);
            modulesView.SortDescriptions.Add(new SortDescription(nameof(CacheModuleViewItem.MemoryBytes), ListSortDirection.Descending));
            modulesView.Filter = MatchesSearch;
            InitializeComponent();
            this.ApplyCaption();
            CacheModulesGrid.ItemsSource = modulesView;
            CacheNavigation.ItemsSource = navigation;
            navigation.Add(new("Overview", EngineLocalization.Get("总览"), "—"));
            CacheNavigation.SelectedIndex = 0;
            ImageCacheEnabledCheckBox.SetBinding(ToggleButton.IsCheckedProperty,
                new Binding(nameof(CvRawFileCacheConfig.IsEnabled)) { Source = imageCacheConfig, Mode = BindingMode.OneWay });
            ImageCacheCountTextBox.SetBinding(TextBox.TextProperty,
                new Binding(nameof(CvRawFileCacheConfig.MaximumEntries)) { Source = imageCacheConfig, Mode = BindingMode.OneWay });
            CameraCacheEnabledCheckBox.SetBinding(ToggleButton.IsCheckedProperty,
                new Binding(nameof(CameraRawBufferCacheConfig.IsEnabled)) { Source = cameraCacheConfig, Mode = BindingMode.OneWay });
            imageCacheConfig.PropertyChanged += CacheConfig_PropertyChanged;
            cameraCacheConfig.PropertyChanged += CacheConfig_PropertyChanged;
            _ = RefreshAsync(showError: true);
        }

        public static void OpenWindow() => OpenWindow(null);

        public static void OpenWindow(long? snapshotSourceId)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OpenWindow(snapshotSourceId));
                return;
            }
            if (instance != null && !instance.isClosed)
            {
                instance.currentSnapshotSourceId = snapshotSourceId;
                instance.SelectPage("Overview");
                _ = instance.RefreshAsync(showError: false);
                if (instance.WindowState == WindowState.Minimized) instance.WindowState = WindowState.Normal;
                instance.Activate();
                return;
            }
            Window? owner = Application.Current.GetActiveWindow();
            if (owner?.IsLoaded != true) owner = null;
            instance = new LocalCalibrationCacheManagerWindow
            {
                currentSnapshotSourceId = snapshotSourceId,
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            };
            instance.Show();
            instance.Activate();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            foreach (CacheModuleViewItem module in modules) module.PropertyChanged -= ModuleSelection_PropertyChanged;
            imageCacheConfig.PropertyChanged -= CacheConfig_PropertyChanged;
            cameraCacheConfig.PropertyChanged -= CacheConfig_PropertyChanged;
            isClosed = true;
            if (ReferenceEquals(instance, this)) instance = null;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (!isBusy) return;
            e.Cancel = true;
            StatusText.Text = EngineLocalization.Get("缓存操作正在进行，请等待操作完成后再关闭窗口。");
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync(showError: true);

        private async void ImageCacheEnabledCheckBox_Click(object sender, RoutedEventArgs e)
            => await SetCacheEnabledAsync("ImageFile", ImageCacheEnabledCheckBox);

        private async void CameraCacheEnabledCheckBox_Click(object sender, RoutedEventArgs e)
            => await SetCacheEnabledAsync("CameraRawBuffer", CameraCacheEnabledCheckBox);

        private async Task SetCacheEnabledAsync(string moduleId, CheckBox checkBox)
        {
            if (isBusy || CacheManagerService.GetById(moduleId) is not ICacheModule module) return;
            bool enabled = checkBox.IsChecked == true;
            SetBusy(true, EngineLocalization.Get("正在读取缓存状态…"));
            try
            {
                await Task.Run(() => module.SetEnabled(enabled));
                ApplySnapshots(await CacheManagerService.ReadSnapshotsAsync());
            }
            catch (Exception ex)
            {
                log.Error($"Change {moduleId} cache setting failed.", ex);
                StatusText.Text = EngineLocalization.Format($"读取缓存状态失败：{ex.Message}");
            }
            finally
            {
                checkBox.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
                if (!isClosed) SetBusy(false, string.Empty);
            }
        }

        private async void CacheConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName != nameof(CvRawFileCacheConfig.IsEnabled) && e.PropertyName != nameof(CvRawFileCacheConfig.MaximumEntries)) || isClosed) return;
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => RefreshAsync(showError: false)).Task.Unwrap();
                return;
            }
            await RefreshAsync(showError: false);
        }

        private async void ApplyImageCacheCountButton_Click(object sender, RoutedEventArgs e)
        {
            if (isBusy || CacheManagerService.GetById("ImageFile") is not ImageFileCacheModule module) return;
            if (!int.TryParse(ImageCacheCountTextBox.Text, out int maximumEntries) || maximumEntries <= 0)
            {
                StatusText.Text = EngineLocalization.Get("请输入大于零的缓存数量。");
                ImageCacheCountTextBox.Focus();
                ImageCacheCountTextBox.SelectAll();
                return;
            }
            SetBusy(true, EngineLocalization.Get("正在读取缓存状态…"));
            try
            {
                await Task.Run(() => module.SetMaximumEntries(maximumEntries));
                ApplySnapshots(await CacheManagerService.ReadSnapshotsAsync());
            }
            catch (Exception ex)
            {
                log.Error("Change image file cache count failed.", ex);
                StatusText.Text = EngineLocalization.Format($"读取缓存状态失败：{ex.Message}");
            }
            finally
            {
                ImageCacheCountTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                if (!isClosed) SetBusy(false, string.Empty);
            }
        }

        private async Task RefreshAsync(bool showError)
        {
            if (isBusy || isClosed) return;
            SetBusy(true, EngineLocalization.Get("正在读取缓存状态…"));
            try
            {
                ApplySnapshots(await CacheManagerService.ReadSnapshotsAsync());
            }
            catch (Exception ex)
            {
                log.Error("Read cache modules failed.", ex);
                if (isClosed) return;
                StatusText.Text = EngineLocalization.Format($"读取缓存状态失败：{ex.Message}");
                if (showError && IsVisible)
                    MessageBox1.Show(this, EngineLocalization.Format($"读取本地缓存失败：{ex.Message}"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!isClosed) SetBusy(false, string.Empty);
            }
        }

        private async void ReleaseSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedModule is not CacheModuleViewItem selected) return;
            if (CacheManagerService.GetById(selected.Id) is ICacheModule module) await ReleaseAsync([module]);
        }

        private async void ReleaseAllButton_Click(object sender, RoutedEventArgs e) => await ReleaseAsync(CacheManagerService.Modules, allModules: true);

        private async void ReleaseMarkedButton_Click(object sender, RoutedEventArgs e)
        {
            ICacheModule[] targets = GetMarkedModules();
            if (targets.Length > 0) await ReleaseAsync(targets);
        }

        private ICacheModule[] GetMarkedModules() => modules.Where(item => item.IsMarkedForRelease)
            .Select(item => CacheManagerService.GetById(item.Id)).OfType<ICacheModule>().ToArray();

        private async void ReleaseModuleRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string id } && CacheManagerService.GetById(id) is ICacheModule module)
                await ReleaseAsync([module]);
        }

        private void SelectVisibleModules_Click(object sender, RoutedEventArgs e)
        {
            foreach (CacheModuleViewItem item in modulesView.Cast<CacheModuleViewItem>()) item.IsMarkedForRelease = true;
        }

        private void ClearModuleSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (CacheModuleViewItem item in modules) item.IsMarkedForRelease = false;
        }

        private void ModuleSelection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!isApplyingSnapshot && e.PropertyName == nameof(CacheModuleViewItem.IsMarkedForRelease)) UpdateReleaseSelection();
        }

        private void UpdateReleaseSelection()
        {
            int selectedCount = modules.Count(item => item.IsMarkedForRelease);
            int visibleCount = modulesView.Cast<CacheModuleViewItem>().Count(item => item.IsMarkedForRelease);
            ReleaseMarkedButton.Content = EngineLocalization.Format($"释放勾选（{selectedCount:N0}）");
            ReleaseMarkedButton.IsEnabled = !isBusy && selectedCount > 0;
            ReleaseSelectionText.Text = selectedCount == visibleCount
                ? EngineLocalization.Format($"已勾选 {selectedCount:N0} 个模块")
                : EngineLocalization.Format($"已勾选 {selectedCount:N0} 个模块，其中 {selectedCount - visibleCount:N0} 个被搜索隐藏");
        }

        private async void ReleaseCurrentSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSnapshotSourceId.HasValue && CacheManagerService.GetById("Snapshot") is SnapshotCacheModule module)
                await ReleaseAsync([module], currentSnapshotSourceId);
        }

        private async Task ReleaseAsync(IReadOnlyList<ICacheModule> targets, long? snapshotSourceId = null, bool allModules = false)
        {
            if (isBusy || targets.Count == 0) return;
            string confirmation = snapshotSourceId.HasValue
                ? EngineLocalization.Get("将释放当前窗口的截图缓存。正在导出的缓冲归还后释放，已保存的截图文件保留。是否继续？")
                : allModules
                ? EngineLocalization.Get("将释放所有缓存模块。\n\n等待正在进行的缓存操作完成，保留磁盘文件，后续使用时会重新加载。是否继续？")
                : EngineLocalization.Format($"将释放以下 {targets.Count:N0} 个缓存模块：\n{string.Join(Environment.NewLine, targets.Select(module => module.Name))}\n\n保留磁盘文件，在用缓冲按各模块规则等待操作完成或归还后释放。是否继续？");
            if (MessageBox1.Show(this, confirmation, EngineLocalization.Get("本地缓存管理"), MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            SetBusy(true, EngineLocalization.Get("正在释放缓存…"));
            try
            {
                IReadOnlyList<CacheModuleReleaseResult> results = snapshotSourceId.HasValue && targets[0] is SnapshotCacheModule snapshotModule
                    ? new[] { await Task.Run(() => snapshotModule.ReleaseSource(snapshotSourceId)) }
                    : await CacheManagerService.ReleaseAllAsync(targets);
                if (!snapshotSourceId.HasValue)
                    foreach (CacheModuleViewItem item in modules.Where(item => results.Any(result => result.ModuleId == item.Id && result.Succeeded)))
                        item.IsMarkedForRelease = false;
                ApplySnapshots(await CacheManagerService.ReadSnapshotsAsync());
                string message = string.Join(Environment.NewLine + Environment.NewLine, results.Select(result => result.Message));
                StatusText.Text = message.Replace(Environment.NewLine, " ");
                MessageBox1.Show(this, message, EngineLocalization.Get("本地缓存管理"), MessageBoxButton.OK,
                    results.All(result => result.Succeeded) ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Error("Release cache modules failed.", ex);
                StatusText.Text = EngineLocalization.Format($"释放缓存失败：{ex.Message}");
                MessageBox1.Show(this, EngineLocalization.Format($"释放本地缓存失败：{ex.Message}"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!isClosed) SetBusy(false, string.Empty);
            }
        }

        private void ApplySnapshots(IReadOnlyList<CacheModuleSnapshot> snapshots)
        {
            if (isClosed) return;
            string selectedId = (CacheNavigation.SelectedItem as CacheNavigationItem)?.Id ?? "Overview";
            ulong totalBytes = snapshots.Where(snapshot => snapshot.Error == null).Aggregate(0UL, (total, snapshot) => total + snapshot.MemoryBytes);
            int readCount = snapshots.Count(snapshot => snapshot.Error == null);
            string totalText = readCount == 0 ? "—" : CacheManagerService.FormatBytes(totalBytes);
            HashSet<string> markedIds = modules.Where(item => item.IsMarkedForRelease).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            isApplyingSnapshot = true;
            try
            {
                foreach (CacheModuleViewItem module in modules) module.PropertyChanged -= ModuleSelection_PropertyChanged;
                modules.Clear();
                foreach (CacheModuleSnapshot snapshot in snapshots)
                {
                    CacheModuleViewItem module = new(snapshot, totalBytes) { IsMarkedForRelease = markedIds.Contains(snapshot.Id) };
                    module.PropertyChanged += ModuleSelection_PropertyChanged;
                    modules.Add(module);
                }
                navigation.Clear();
                navigation.Add(new("Overview", EngineLocalization.Get("总览"), totalText));
                foreach (CacheModuleViewItem item in modules) navigation.Add(new(item.Id, item.Name, item.MemoryText));
                CacheNavigation.SelectedItem = navigation.FirstOrDefault(item => item.Id == selectedId) ?? navigation[0];
            }
            finally { isApplyingSnapshot = false; }
            TotalMemoryText.Text = totalText;
            ModuleCountText.Text = EngineLocalization.Format($"已读取 {readCount:N0} / {snapshots.Count:N0} 个模块");
            OverviewWarningText.Visibility = readCount == snapshots.Count ? Visibility.Collapsed : Visibility.Visible;
            OverviewWarningText.Text = EngineLocalization.Get("部分模块读取失败，当前总量不完整；失败模块的内存占用未统计。");
            StatusText.Text = EngineLocalization.Format($"最后刷新：{DateTime.Now:HH:mm:ss}");
            if (snapshots.Any(snapshot => snapshot.Error != null))
                StatusText.Text += " · " + EngineLocalization.Get("读取失败");
            UpdateModuleDetails();
            UpdateEmptyState();
        }

        private bool MatchesSearch(object value)
        {
            if (value is not CacheModuleViewItem item) return false;
            string query = CacheSearchBox?.Text.Trim() ?? string.Empty;
            return query.Length == 0 || item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || item.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || item.Snapshot.Entries.Any(entry => entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || entry.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        private void CacheSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchPlaceholderText == null || CacheModulesGrid == null) return;
            SearchPlaceholderText.Visibility = string.IsNullOrEmpty(CacheSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            modulesView.Refresh();
            UpdateModuleDetails();
            UpdateEmptyState();
        }

        private CacheModuleViewItem? SelectedModule => modules.FirstOrDefault(item => item.Id == (CacheNavigation.SelectedItem as CacheNavigationItem)?.Id);

        private void CacheNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isApplyingSnapshot || SelectedModuleTitleText == null) return;
            CacheSearchBox.Clear();
            UpdateModuleDetails();
        }

        private void ViewModule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string id }) SelectPage(id);
        }

        private void SelectPage(string id)
            => CacheNavigation.SelectedItem = navigation.FirstOrDefault(item => item.Id == id) ?? navigation.FirstOrDefault();

        private void UpdateModuleDetails()
        {
            CacheModuleViewItem? item = SelectedModule;
            CacheModuleSnapshot? selected = item?.Snapshot;
            bool overview = selected == null;
            bool screenshot = selected?.Id == "Snapshot";
            SnapshotScopeText.Visibility = screenshot ? Visibility.Visible : Visibility.Collapsed;
            OverviewPanel.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            OverviewSummaryPanel.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            DetailsSummaryPanel.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            CacheSettingsPanel.Visibility = overview || selected?.CanToggle == true ? Visibility.Visible : Visibility.Collapsed;
            CacheSettingsTitle.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            DetailsPanel.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            ReleaseMarkedButton.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            ReleaseAllButton.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            ReleaseSelectedButton.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            ReleaseSelectedButton.Content = EngineLocalization.Get(screenshot ? "释放全部截图缓存" : "释放本模块缓存");
            ReleaseCurrentSnapshotButton.Visibility = screenshot && currentSnapshotSourceId.HasValue ? Visibility.Visible : Visibility.Collapsed;
            ReleaseCurrentSnapshotButton.IsEnabled = !isBusy && selected?.Entries.Any(entry => entry.SourceId == currentSnapshotSourceId && entry.MemoryBytes > 0) == true;
            SelectedModuleTitleText.Text = selected?.Name ?? EngineLocalization.Get("总览");
            SelectedModuleDescriptionText.Text = selected?.Description ?? string.Empty;
            SelectedModuleSummaryText.Text = item == null ? string.Empty
                : EngineLocalization.Format($"内存占用：{item.MemoryText} · {item.UsageText}") + Environment.NewLine + selected!.DetailSummary;
            SelectedModuleErrorText.Text = selected?.Error ?? string.Empty;
            SelectedModuleErrorText.Visibility = selected?.Error == null ? Visibility.Collapsed : Visibility.Visible;
            if (detailsModuleId != selected?.Id)
            {
                detailsModuleId = selected?.Id;
                ConfigureDetailColumns(detailsModuleId);
            }
            string query = CacheSearchBox.Text.Trim();
            CacheEntryViewItem[] entries = selected?.Entries.Select(entry => new CacheEntryViewItem(entry, currentSnapshotSourceId))
                .Where(entry => query.Length == 0 || entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || entry.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase) || entry.BufferInfo.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
            CacheDetailsGrid.ItemsSource = entries;
            ImageCacheOptionsPanel.Visibility = overview || selected?.Id == "ImageFile" ? Visibility.Visible : Visibility.Collapsed;
            CameraCacheOptionsPanel.Visibility = overview || selected?.Id == "CameraRawBuffer" ? Visibility.Visible : Visibility.Collapsed;
            EmptyDetailsText.Visibility = entries.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptyDetailsText.Text = EngineLocalization.Get(selected?.Error != null ? "读取失败，请刷新重试。"
                : query.Length > 0 ? "没有匹配的缓存条目。" : screenshot ? "暂无截图缓冲来源。"
                : selected?.IsEnabled == false ? "已停用，当前没有保留的缓存。" : "当前模块暂无缓存。");
            ReleaseSelectedButton.IsEnabled = !isBusy && item?.CanRelease == true;
            UpdateReleaseSelection();
        }

        private void ConfigureDetailColumns(string? moduleId)
        {
            CacheDetailsGrid.Columns.Clear();
            if (moduleId == null) return;
            bool screenshot = moduleId == "Snapshot";
            bool camera = moduleId == "CameraRawBuffer";
            AddDetailColumn(screenshot ? "来源窗口 / 视图" : camera ? "相机" : "缓存条目", nameof(CacheEntryViewItem.Name),
                screenshot || camera ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(120), minimum: screenshot ? 200 : 150);
            if (screenshot)
            {
                AddDetailColumn("空闲缓冲规格", nameof(CacheEntryViewItem.BufferInfo), new(160));
                AddDetailColumn("空闲可释放", nameof(CacheEntryViewItem.IdleText), new(95), true, nameof(CacheEntryViewItem.IdleBytes));
                AddDetailColumn("正在使用", nameof(CacheEntryViewItem.ActiveText), new(95), true, nameof(CacheEntryViewItem.ActiveBytes));
                AddDetailColumn("待归还释放", nameof(CacheEntryViewItem.PendingText), new(95), true, nameof(CacheEntryViewItem.PendingReleaseBytes));
            }
            else
            {
                if (!camera)
                {
                    AddDetailColumn("文件路径", nameof(CacheEntryViewItem.FilePath), new(1, DataGridLengthUnitType.Star), minimum: 170);
                    AddDetailColumn("文件大小", nameof(CacheEntryViewItem.FileSizeText), new(95), true, nameof(CacheEntryViewItem.FileBytes));
                }
                AddDetailColumn("内存占用", nameof(CacheEntryViewItem.MemoryText), new(100), true, nameof(CacheEntryViewItem.MemoryBytes));
                if (!camera)
                {
                    AddDetailColumn("命中次数", nameof(CacheEntryViewItem.HitCount), new(85), true);
                    AddDetailColumn("活动引用", nameof(CacheEntryViewItem.ActiveReferences), new(85), true);
                }
            }
            AddDetailColumn("使用状态", nameof(CacheEntryViewItem.UsageText), new(155));
        }

        private void AddDetailColumn(string header, string property, DataGridLength width, bool numeric = false, string? sortProperty = null, double minimum = 0)
        {
            Style style = new(typeof(TextBlock), (Style)FindResource(numeric ? "CacheNumberTextStyle" : "CacheCellTextStyle"));
            style.Setters.Add(new Setter(ToolTipProperty, new Binding(property)));
            CacheDetailsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = EngineLocalization.Get(header), Binding = new Binding(property), Width = width,
                MinWidth = minimum > 0 ? minimum : width.IsAbsolute ? width.Value : 80,
                SortMemberPath = sortProperty ?? property, ElementStyle = style,
            });
        }

        private void UpdateEmptyState() => EmptyModulesText.Visibility = modulesView.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

        private void SetBusy(bool busy, string loadingText)
        {
            isBusy = busy;
            CacheContentPanel.IsEnabled = !busy;
            RefreshButton.IsEnabled = !busy;
            ReleaseAllButton.IsEnabled = !busy;
            ImageCacheEnabledCheckBox.IsEnabled = !busy;
            CameraCacheEnabledCheckBox.IsEnabled = !busy;
            ImageCacheCountTextBox.IsEnabled = !busy;
            ApplyImageCacheCountButton.IsEnabled = !busy;
            LoadingText.Text = loadingText;
            LoadingOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            UpdateModuleDetails();
        }
    }
}
