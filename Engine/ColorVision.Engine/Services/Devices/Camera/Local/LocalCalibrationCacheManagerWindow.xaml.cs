using ColorVision.Core;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using cvColorVision;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal sealed class LocalCalibrationCacheViewItem
    {
        public required string CalibrationTypeText { get; init; }
        public required string FilePath { get; init; }
        public required string ResidentMemoryText { get; init; }
        public ulong HitCount { get; init; }
        public required string UsageText { get; init; }
    }

    public partial class LocalCalibrationCacheManagerWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalCalibrationCacheManagerWindow));
        private static LocalCalibrationCacheManagerWindow? instance;
        private readonly ObservableCollection<LocalCalibrationCacheViewItem> items = new();
        private bool isBusy;
        private bool isClosed;

        public LocalCalibrationCacheManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            CacheDataGrid.ItemsSource = items;
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
                if (instance.WindowState == WindowState.Minimized)
                {
                    instance.WindowState = WindowState.Normal;
                }
                instance.Activate();
                return;
            }

            instance = null;
            Window? owner = Application.Current.MainWindow;
            if (owner?.IsLoaded != true)
            {
                owner = null;
            }
            instance = new LocalCalibrationCacheManagerWindow
            {
                Owner = owner,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
            };
            instance.Show();
            instance.Activate();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            await RefreshAsync(showError: true);
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            isClosed = true;
            if (ReferenceEquals(instance, this))
            {
                instance = null;
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (!isBusy) return;

            e.Cancel = true;
            StatusText.Text = EngineLocalization.Get("缓存操作正在进行，请等待操作完成后再关闭窗口。");
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync(showError: true);
        }

        private async Task RefreshAsync(bool showError)
        {
            if (isBusy) return;

            SetBusy(true, EngineLocalization.Get("正在读取缓存状态…"));
            try
            {
                CalibrationSharedCacheSnapshot snapshot = await Task.Run(LocalCalibrationCacheService.GetSnapshot);
                if (isClosed) return;
                ApplySnapshot(snapshot);
            }
            catch (Exception ex)
            {
                log.Error("Read local calibration cache snapshot failed.", ex);
                if (isClosed) return;
                StatusText.Text = EngineLocalization.Format($"读取缓存状态失败：{ex.Message}");
                if (showError)
                {
                    MessageBox1.Show(this, EngineLocalization.Format($"读取本地校正缓存失败：{ex.Message}"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (!isClosed)
                {
                    SetBusy(false, string.Empty);
                }
            }
        }

        private async void ReleaseAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (isBusy) return;

            string confirmation = EngineLocalization.Get("将释放所有相机的本地校正上下文，并清理 opencv_helper 可释放的共享校正文件缓存。\n\n正在执行的校正会先完成；仍被其他活动上下文使用的内存不会被强制释放。是否继续？");
            if (MessageBox1.Show(this, confirmation, EngineLocalization.Get("本地校正缓存管理"), MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }

            SetBusy(true, EngineLocalization.Get("正在等待校正执行结束并释放缓存…"));
            Exception? refreshError = null;
            try
            {
                LocalCalibrationCacheReleaseSummary summary = await LocalCalibrationCacheService.ReleaseAllAsync();
                if (isClosed) return;
                try
                {
                    CalibrationSharedCacheSnapshot snapshot = await Task.Run(LocalCalibrationCacheService.GetSnapshot);
                    if (isClosed) return;
                    ApplySnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    refreshError = ex;
                    log.Error("Refresh local calibration cache snapshot after release failed.", ex);
                }
                if (isClosed) return;

                string message = BuildReleaseMessage(summary, refreshError);
                bool hasActiveEntries = summary.NativeRelease?.ActiveEntryCount > 0;
                MessageBoxImage image = summary.Succeeded && !hasActiveEntries && refreshError == null
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning;
                StatusText.Text = message.Replace(Environment.NewLine, " ");
                MessageBox1.Show(this, message, EngineLocalization.Get("本地校正缓存管理"), MessageBoxButton.OK, image);
            }
            catch (Exception ex)
            {
                log.Error("Release all local calibration caches failed.", ex);
                if (isClosed) return;
                StatusText.Text = EngineLocalization.Format($"释放缓存失败：{ex.Message}");
                MessageBox1.Show(this, EngineLocalization.Format($"释放本地校正缓存失败：{ex.Message}"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!isClosed)
                {
                    SetBusy(false, string.Empty);
                }
            }
        }

        private void ApplySnapshot(CalibrationSharedCacheSnapshot snapshot)
        {
            if (isClosed) return;

            items.Clear();
            foreach (CalibrationSharedCacheEntry entry in snapshot.Entries
                .OrderBy(item => item.CalibrationType)
                .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(new LocalCalibrationCacheViewItem
                {
                    CalibrationTypeText = GetCalibrationTypeText(entry.CalibrationType),
                    FilePath = entry.FilePath,
                    ResidentMemoryText = FormatBytes(entry.EstimatedMemoryBytes),
                    HitCount = entry.HitCount,
                    UsageText = GetUsageText(entry),
                });
            }

            CalibrationSharedCacheStatistics statistics = snapshot.Statistics;
            EntryCountText.Text = EngineLocalization.Format($"缓存文件：{statistics.EntryCount:N0}");
            ResidentMemoryText.Text = EngineLocalization.Format($"驻留内存：{FormatBytes(statistics.EstimatedMemoryBytes)}");
            BudgetText.Text = statistics.BudgetBytes == 0
                ? EngineLocalization.Get("缓存预算：未限制")
                : EngineLocalization.Format($"缓存预算：{FormatBytes(statistics.BudgetBytes)}");
            HitSummaryText.Text = EngineLocalization.Format($"命中：{statistics.HitCount:N0} / 未命中：{statistics.MissCount:N0}");

            int activeEntries = snapshot.Entries.Count(entry => entry.ActiveOwnerCount > 0);
            ulong activeOwners = snapshot.Entries.Aggregate(0UL, (total, entry) => total + entry.ActiveOwnerCount);
            StatusText.Text = activeEntries == 0
                ? EngineLocalization.Format($"最后刷新：{DateTime.Now:HH:mm:ss}。当前没有缓存被活动上下文占用。")
                : EngineLocalization.Format($"最后刷新：{DateTime.Now:HH:mm:ss}。{activeEntries} 个缓存文件仍有 {activeOwners} 个活动引用。");
        }

        private static string BuildReleaseMessage(LocalCalibrationCacheReleaseSummary summary, Exception? refreshError)
        {
            List<string> lines = new()
            {
                EngineLocalization.Format($"已检查 {summary.DeviceCount} 台相机，释放 {summary.ContextsReleased} 个本地校正上下文缓存项。"),
            };

            if (summary.NativeRelease is CalibrationSharedCacheReleaseResult nativeRelease)
            {
                lines.Add(EngineLocalization.Format($"共享文件缓存已移除 {nativeRelease.ReleasedEntryCount} 项，涉及驻留内存约 {FormatBytes(nativeRelease.ReleasedEstimatedMemoryBytes)}。"));
                if (nativeRelease.ActiveEntryCount > 0)
                {
                    lines.Add(EngineLocalization.Format($"其中仍有 {nativeRelease.ActiveEntryCount} 项被 {nativeRelease.ActiveOwnerCount} 个活动引用使用，约 {FormatBytes(nativeRelease.ActiveEstimatedMemoryBytes)} 暂未物理释放。完成相关执行后可再次释放。"));
                }
                else
                {
                    lines.Add(EngineLocalization.Get("没有共享文件缓存仍被活动上下文占用。"));
                }
            }
            else
            {
                lines.Add(EngineLocalization.Get("opencv_helper 共享文件缓存未能执行释放。"));
            }

            if (summary.Errors.Count > 0)
            {
                lines.Add(EngineLocalization.Get("释放错误：") + string.Join(EngineLocalization.Get("；"), summary.Errors.Select(error => $"{error.DeviceCode}: {error.Message}")));
            }
            if (refreshError != null)
            {
                lines.Add(EngineLocalization.Format($"释放后刷新失败：{refreshError.Message}"));
            }
            return string.Join(Environment.NewLine, lines);
        }

        private void SetBusy(bool busy, string loadingText)
        {
            isBusy = busy;
            RefreshButton.IsEnabled = !busy;
            ReleaseAllButton.IsEnabled = !busy;
            LoadingText.Text = loadingText;
            LoadingOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string GetCalibrationTypeText(int calibrationType)
        {
            return Enum.IsDefined(typeof(CalibrationType), calibrationType)
                ? EngineLocalization.Get(((CalibrationType)calibrationType).ToString())
                : EngineLocalization.Format($"未知 ({calibrationType})");
        }

        private static string GetUsageText(CalibrationSharedCacheEntry entry)
        {
            if ((entry.Flags & CalibrationSharedCacheEntryStates.Loading) != 0)
            {
                return EngineLocalization.Get("正在加载");
            }
            if (entry.ActiveOwnerCount > 0)
            {
                return EngineLocalization.Format($"仍被使用（{entry.ActiveOwnerCount} 个引用）");
            }
            return (entry.Flags & CalibrationSharedCacheEntryStates.Ready) != 0
                ? EngineLocalization.Get("已缓存（可释放）")
                : EngineLocalization.Get("等待加载");
        }

        private static string FormatBytes(ulong bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return unitIndex == 0 ? $"{bytes:N0} {units[unitIndex]}" : $"{value:N2} {units[unitIndex]}";
        }
    }
}
