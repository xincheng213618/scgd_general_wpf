using ColorVision.Common.ThirdPartyApps;
using ColorVision.Engine.Media;
using ColorVision.Themes;
using ColorVision.ToolPlugins.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.ToolPlugins.CvRawExport
{
    public sealed class CvRawExportAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            yield return new ThirdPartyAppInfo
            {
                Name = "CVRAW 批量导出 TIFF",
                Group = ThirdPartyAppGroupNames.CommonTools,
                Category = ThirdPartyAppCategory.Internal,
                RequiredPermission = PermissionMode.Guest,
                Order = -894,
                IconGlyph = ThirdPartyAppIconGlyphs.Export,
                LaunchAction = CvRawExportWindow.ShowWindow
            };
        }
    }

    public partial class CvRawExportWindow : Window
    {
        private CancellationTokenSource? _cancellation;
        private CvRawExportProgress? _latestProgress;
        private bool _closeWhenFinished;

        public CvRawExportWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            ParallelismComboBox.ItemsSource = Enumerable.Range(1, 32);
            ParallelismComboBox.SelectedItem = CvRawBatchExporter.DefaultParallelism;
        }

        public static void ShowWindow()
        {
            CvRawExportWindow? existing = Application.Current.Windows.OfType<CvRawExportWindow>().FirstOrDefault();
            if (existing != null)
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }
            new CvRawExportWindow { Owner = Application.Current.GetActiveWindow() }.Show();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new() { Title = "选择包含 CVRAW 文件的文件夹", Multiselect = false };
            if (dialog.ShowDialog(this) == true) FolderTextBox.Text = dialog.FolderName;
        }

        private async void Execute_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellation != null) return;
            string folder = FolderTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder))
            {
                StatusText.Text = "请先选择文件夹。";
                return;
            }
            int parallelism = (int)ParallelismComboBox.SelectedItem;
            using CancellationTokenSource cancellation = new();
            _cancellation = cancellation;
            _latestProgress = null;
            OptionsPanel.IsEnabled = ParallelismPanel.IsEnabled = ExecuteButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ErrorsGrid.ItemsSource = null;
            CurrentFileText.Text = string.Empty;
            StatusText.Text = "正在检索 CVRAW 文件…";
            ExportProgressBar.Value = 0;
            ExportProgressBar.IsIndeterminate = true;
            // Poll a single snapshot so large batches cannot flood the UI dispatcher.
            DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (_, _) => RefreshProgress();
            timer.Start();
            try
            {
                CvRawExportResult result = await Task.Run(() => CvRawBatchExporter.ExportFolder(folder, parallelism,
                    progress => Interlocked.Exchange(ref _latestProgress, progress), cancellation.Token));
                RefreshProgress();
                ErrorsGrid.ItemsSource = result.Files.Where(file => file.Status == CvRawExportStatus.Failed).ToArray();
                StatusText.Text = $"{(result.Cancelled ? "已取消" : "已完成")}：共 {result.Total} 个，已处理 {result.Files.Count} 个，成功 {result.Exported} 个，跳过 {result.Skipped} 个，失败 {result.Failed} 个。";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"执行失败：{ex.Message}";
            }
            finally
            {
                timer.Stop();
                ExportProgressBar.IsIndeterminate = false;
                _cancellation = null;
                OptionsPanel.IsEnabled = ParallelismPanel.IsEnabled = ExecuteButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                if (_closeWhenFinished) Close();
            }
        }

        private void RefreshProgress()
        {
            CvRawExportProgress? progress = Volatile.Read(ref _latestProgress);
            if (progress == null) return;
            ExportProgressBar.IsIndeterminate = false;
            ExportProgressBar.Maximum = Math.Max(1, progress.Total);
            ExportProgressBar.Value = progress.Completed;
            CurrentFileText.Text = progress.CurrentFile;
            StatusText.Text = _cancellation?.IsCancellationRequested == true
                ? "正在取消，等待当前文件处理结束…"
                : $"已处理 {progress.Completed} / {progress.Total}，成功 {progress.Exported}，跳过 {progress.Skipped}，失败 {progress.Failed}。";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => CancelRun();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void CancelRun()
        {
            _cancellation?.Cancel();
            CancelButton.IsEnabled = false;
            StatusText.Text = "正在取消，等待当前文件处理结束…";
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_cancellation == null) return;
            e.Cancel = true;
            _closeWhenFinished = true;
            CancelRun();
        }
    }
}
