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
        private readonly CvRawFileCacheConfig imageCacheConfig;
        private readonly ICollectionView modulesView;
        private bool isBusy;
        private bool isClosed;
        private bool isApplyingSnapshot;

        public LocalCalibrationCacheManagerWindow()
        {
            imageCacheConfig = CvRawFileCacheConfig.Current;
            modulesView = CollectionViewSource.GetDefaultView(modules);
            modulesView.SortDescriptions.Add(new SortDescription(nameof(CacheModuleViewItem.MemoryBytes), ListSortDirection.Descending));
            modulesView.Filter = MatchesSearch;
            InitializeComponent();
            this.ApplyCaption();
            CacheModulesGrid.ItemsSource = modulesView;
            ImageCacheEnabledCheckBox.SetBinding(ToggleButton.IsCheckedProperty,
                new Binding(nameof(CvRawFileCacheConfig.IsEnabled)) { Source = imageCacheConfig, Mode = BindingMode.OneWay });
            ImageCacheCountTextBox.SetBinding(TextBox.TextProperty,
                new Binding(nameof(CvRawFileCacheConfig.MaximumEntries)) { Source = imageCacheConfig, Mode = BindingMode.OneWay });
            imageCacheConfig.PropertyChanged += ImageCacheConfig_PropertyChanged;
        }

        public static void OpenWindow()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(OpenWindow);
                return;
            }
            if (instance != null && !instance.isClosed)
            {
                if (instance.WindowState == WindowState.Minimized) instance.WindowState = WindowState.Normal;
                instance.Activate();
                return;
            }
            Window? owner = Application.Current.MainWindow;
            if (owner?.IsLoaded != true) owner = null;
            instance = new LocalCalibrationCacheManagerWindow
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            };
            instance.Show();
            instance.Activate();
        }

        private async void Window_Initialized(object sender, EventArgs e) => await RefreshAsync(showError: true);

        private void Window_Closed(object? sender, EventArgs e)
        {
            imageCacheConfig.PropertyChanged -= ImageCacheConfig_PropertyChanged;
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
        {
            if (isBusy || CacheManagerService.GetById("ImageFile") is not ICacheModule module) return;
            bool enabled = ImageCacheEnabledCheckBox.IsChecked == true;
            SetBusy(true, EngineLocalization.Get("正在读取缓存状态…"));
            try
            {
                await Task.Run(() => module.SetEnabled(enabled));
                ApplySnapshots(await CacheManagerService.ReadSnapshotsAsync());
            }
            catch (Exception ex)
            {
                log.Error("Change image file cache setting failed.", ex);
                StatusText.Text = EngineLocalization.Format($"读取缓存状态失败：{ex.Message}");
            }
            finally
            {
                ImageCacheEnabledCheckBox.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
                if (!isClosed) SetBusy(false, string.Empty);
            }
        }

        private async void ImageCacheConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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
            if (CacheModulesGrid.SelectedItem is not CacheModuleViewItem selected) return;
            if (CacheManagerService.GetById(selected.Id) is ICacheModule module) await ReleaseAsync(module);
        }

        private async void ReleaseAllButton_Click(object sender, RoutedEventArgs e) => await ReleaseAsync(null);

        private async Task ReleaseAsync(ICacheModule? selectedModule)
        {
            if (isBusy) return;
            string confirmation = selectedModule == null
                ? EngineLocalization.Get("将释放所有缓存模块。\n\n等待正在进行的缓存操作完成，保留磁盘文件，后续使用时会重新加载。是否继续？")
                : EngineLocalization.Format($"将释放“{selectedModule.Name}”模块的缓存。\n\n等待正在进行的缓存操作完成，保留磁盘文件，后续使用时会重新加载。是否继续？");
            if (MessageBox1.Show(this, confirmation, EngineLocalization.Get("本地缓存管理"), MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            SetBusy(true, EngineLocalization.Get("正在释放缓存…"));
            try
            {
                IReadOnlyList<CacheModuleReleaseResult> results = selectedModule == null
                    ? await CacheManagerService.ReleaseAllAsync()
                    : new[] { await CacheManagerService.ReleaseAsync(selectedModule) };
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
            string? selectedId = (CacheModulesGrid.SelectedItem as CacheModuleViewItem)?.Id;
            ulong totalBytes = snapshots.Aggregate(0UL, (total, snapshot) => total + snapshot.MemoryBytes);
            isApplyingSnapshot = true;
            try
            {
                modules.Clear();
                foreach (CacheModuleSnapshot snapshot in snapshots) modules.Add(new CacheModuleViewItem(snapshot, totalBytes));
                CacheModulesGrid.SelectedItem = modulesView.Cast<CacheModuleViewItem>().FirstOrDefault(item => item.Id == selectedId)
                    ?? modulesView.Cast<CacheModuleViewItem>().FirstOrDefault();
            }
            finally { isApplyingSnapshot = false; }
            TotalMemoryText.Text = CacheManagerService.FormatBytes(totalBytes);
            ModuleCountText.Text = EngineLocalization.Format($"缓存模块：{snapshots.Count:N0}");
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
            if (CacheModulesGrid.SelectedItem == null) CacheModulesGrid.SelectedItem = modulesView.Cast<CacheModuleViewItem>().FirstOrDefault();
            UpdateModuleDetails();
            UpdateEmptyState();
        }

        private void CacheModulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isApplyingSnapshot && SelectedModuleTitleText != null) UpdateModuleDetails();
        }

        private void UpdateModuleDetails()
        {
            CacheModuleSnapshot? selected = (CacheModulesGrid.SelectedItem as CacheModuleViewItem)?.Snapshot;
            SelectedModuleTitleText.Text = selected?.Name ?? EngineLocalization.Get("模块详情");
            SelectedModuleDescriptionText.Text = selected?.Description ?? EngineLocalization.Get("选择模块以查看缓存详情。");
            SelectedModuleSummaryText.Text = selected?.DetailSummary ?? string.Empty;
            SelectedModuleErrorText.Text = selected?.Error ?? string.Empty;
            SelectedModuleErrorText.Visibility = selected?.Error == null ? Visibility.Collapsed : Visibility.Visible;
            CacheDetailsGrid.ItemsSource = selected?.Entries.Select(entry => new CacheEntryViewItem(entry)).ToArray();
            ImageCacheOptionsPanel.Visibility = selected?.CanToggle == true ? Visibility.Visible : Visibility.Collapsed;
            EmptyDetailsText.Visibility = selected?.Entries.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptyDetailsText.Text = EngineLocalization.Get(selected == null ? "选择模块以查看缓存详情。" : "当前模块没有缓存文件。");
            ReleaseSelectedButton.IsEnabled = !isBusy && selected != null
                && (selected.MemoryBytes > 0 || selected.EntryCount > 0 || selected.ActiveReferences > 0 || selected.Error != null);
        }

        private void UpdateEmptyState() => EmptyModulesText.Visibility = modulesView.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

        private void SetBusy(bool busy, string loadingText)
        {
            isBusy = busy;
            RefreshButton.IsEnabled = !busy;
            ReleaseAllButton.IsEnabled = !busy;
            ImageCacheEnabledCheckBox.IsEnabled = !busy;
            ImageCacheCountTextBox.IsEnabled = !busy;
            ApplyImageCacheCountButton.IsEnabled = !busy;
            LoadingText.Text = loadingText;
            LoadingOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            UpdateModuleDetails();
        }
    }
}
