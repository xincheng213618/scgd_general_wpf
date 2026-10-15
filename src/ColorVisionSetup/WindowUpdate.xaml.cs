using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ColorVisionSetup
{
    public partial class WindowUpdate : Window
    {
        private readonly SetupViewModel _model = new SetupViewModel();
        private CancellationTokenSource _operation;
        private InstallerPackage _package;
        private bool _closeAfterCancel;
        private bool _loaded;
        private bool _animationStarted;

        public WindowUpdate()
        {
            InitializeComponent();
            DataContext = _model;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded) return;
            _loaded = true;
            if (SystemParameters.ClientAreaAnimation && System.Windows.Media.RenderCapability.Tier > 0)
            {
                ((Storyboard)Resources["SpectrumDrift"]).Begin(this, true);
                _animationStarted = true;
            }
            await RunAsync(false);
        }

        private async void Primary_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.CanAct) return;
            if (_model.Stage == DownloadStage.Ready && _package != null) await OpenPackageAsync();
            else await RunAsync(true);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_model.CanRefresh) await RunAsync(false);
        }

        private async Task RunAsync(bool download)
        {
            if (_operation != null || _model.Stage == DownloadStage.Launching) return;
            _operation = new CancellationTokenSource();
            CancellationToken token = _operation.Token;
            _package = null;
            _model.CancelRequested = false;
            _model.LatestVersion = null;
            _model.Progress = 0;
            _model.IsIndeterminate = true;
            _model.Stage = DownloadStage.Checking;
            _model.Status = "正在连接版本服务器";
            _model.Detail = "即将为你获取 ColorVision 最新版本。";
            _model.TransferText = "检查最新版本";
            try
            {
                using (var client = new ReleaseClient())
                {
                    Version version = await client.GetLatestAsync(token);
                    token.ThrowIfCancellationRequested();
                    _model.LatestVersion = version;
                    if (!download)
                    {
                        _model.Stage = DownloadStage.Available;
                        _model.Status = "最新版本，准备就绪";
                        _model.Detail = "点击下载，获取最新的 ColorVision 完整安装包。";
                        _model.TransferText = "等待开始下载";
                        return;
                    }

                    _model.Stage = DownloadStage.Downloading;
                    _model.Status = "正在下载最新版本";
                    _model.Detail = "下载完成后，你可以直接打开安装包。";
                    _model.TransferText = "正在连接下载服务器";
                    string destination = SetupFiles.CreatePackagePath(version);
                    var progress = new Progress<TransferProgress>(value =>
                    {
                        if (_operation == null || _operation.Token != token || token.IsCancellationRequested || _model.Stage != DownloadStage.Downloading) return;
                        _model.IsIndeterminate = !value.Total.HasValue;
                        _model.Progress = value.Total > 0 ? 100d * value.Received / value.Total.Value : 0;
                        string size = (value.Received / 1048576d).ToString("F1") + " MB";
                        if (value.Total.HasValue) size += " / " + (value.Total.Value / 1048576d).ToString("F1") + " MB";
                        _model.TransferText = size + "   ·   " + (value.BytesPerSecond / 1048576d).ToString("F1") + " MB/s";
                    });
                    await client.DownloadAsync(version, destination, progress, token);
                    _model.Stage = DownloadStage.Verifying;
                    _model.Status = "正在校验下载文件";
                    _model.Detail = "确认安装包完整且来自 ColorVision。";
                    _model.IsIndeterminate = true;
                    var package = await Task.Run(() => InstallerPackage.Verify(destination, version, token), token);
                    token.ThrowIfCancellationRequested();
                    _package = package;
                    _model.Stage = DownloadStage.Ready;
                    _model.Progress = 100;
                    _model.Status = "下载完成";
                    _model.Detail = "一切就绪。打开安装包，按提示完成安装。";
                    _model.TransferText = "文件完整性与发行签名已校验";
                    SetupFiles.Log("Package verified: " + package.Path + ", version=" + package.Version + ", SHA256=" + package.Sha256);
                }
            }
            catch (OperationCanceledException)
            {
                _model.Stage = DownloadStage.Cancelled;
                _model.Status = "已取消";
                _model.Detail = "准备好后，随时可以重新下载。";
                _model.TransferText = "下载已停止";
                _model.Progress = 0;
            }
            catch (Exception ex) { ShowFailure(ex); }
            finally
            {
                _model.IsIndeterminate = false;
                _operation.Dispose();
                _operation = null;
                if (_closeAfterCancel) Close();
            }
        }

        private async Task OpenPackageAsync()
        {
            _model.Stage = DownloadStage.Launching;
            _model.Status = "正在打开安装包";
            _model.Detail = "请在 Windows 权限提示中确认。";
            _model.IsIndeterminate = true;
            try
            {
                await Task.Run(() => _package.Launch());
                _model.Stage = DownloadStage.Opened;
                _model.Status = "安装包已打开";
                _model.Detail = "接下来请在安装器窗口中继续。";
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _model.Stage = DownloadStage.Ready;
                _model.Status = "已取消打开安装包";
                _model.Detail = "文件已下载，随时可以再次打开。";
            }
            catch (Exception ex) { ShowFailure(ex); }
            finally { _model.IsIndeterminate = false; }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.CanCancel) return;
            _model.CancelRequested = true;
            _model.Status = "正在取消";
            _operation?.Cancel();
        }

        private void ShowFailure(Exception ex)
        {
            SetupFiles.Log(ex.ToString());
            _model.Stage = DownloadStage.Error;
            _model.Progress = 0;
            _model.Status = "暂时无法完成下载";
            _model.Detail = "请检查网络后重试。详细原因可在日志中查看。";
            _model.TransferText = ex is System.IO.InvalidDataException ? "文件校验失败，请重新下载" : "连接或文件操作失败";
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.CanOpenFolder || _package == null) return;
            try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + _package.Path + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { SetupFiles.Log(ex.ToString()); _model.Detail = "无法打开文件夹。安装包位置：" + _package.Path; }
        }

        private void OpenLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetupFiles.Log("Log opened by user.");
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + SetupFiles.LogPath + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { _model.Detail = "无法打开日志：" + ex.Message; }
        }

        private void Caption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_model.Stage == DownloadStage.Launching) { e.Cancel = true; return; }
            if (_operation == null) return;
            e.Cancel = true;
            _closeAfterCancel = true;
            _operation.Cancel();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_animationStarted) ((Storyboard)Resources["SpectrumDrift"]).Remove(this);
        }
    }
}
