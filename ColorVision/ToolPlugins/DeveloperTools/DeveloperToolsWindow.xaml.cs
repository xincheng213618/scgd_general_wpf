using ColorVision.Common.ThirdPartyApps;
using ColorVision.Engine.Services.DeveloperTools;
using ColorVision.Themes;
using ColorVision.ToolPlugins.ThirdPartyApps;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.ToolPlugins.DeveloperTools
{
    public sealed class DeveloperToolsAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            yield return new ThirdPartyAppInfo
            {
                Name = "开发工具管理",
                Group = ThirdPartyAppGroupNames.CommonTools,
                Category = ThirdPartyAppCategory.Internal,
                RequiredPermission = PermissionMode.Guest,
                Order = -896,
                IconGlyph = "\uE943",
                LaunchAction = () =>
                {
                    var existing = Application.Current.Windows.OfType<DeveloperToolsWindow>().FirstOrDefault();
                    if (existing != null)
                    {
                        if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                        existing.Activate();
                        return;
                    }
                    new DeveloperToolsWindow { Owner = Application.Current.GetActiveWindow() }.Show();
                },
            };
        }
    }

    public partial class DeveloperToolsWindow : Window
    {
        private readonly DeveloperToolDiscoveryService _discovery = new();
        private readonly DeveloperToolCatalogService _catalog = new();
        private readonly CancellationTokenSource _lifetime = new();
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
        private bool _refreshing;
        private bool _operationBusy;
        private bool _closed;

        public DeveloperToolPageModel Python { get; } = new(DeveloperToolKind.Python);
        public DeveloperToolPageModel NodeJs { get; } = new(DeveloperToolKind.NodeJs);

        public DeveloperToolsWindow()
        {
            InitializeComponent();
            DataContext = this;
            this.ApplyCaption();
            _timer.Tick += Timer_Tick;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _timer.Start();
            await RefreshAsync();
        }

        private async void Window_Activated(object? sender, EventArgs e)
        {
            if (IsLoaded) await RefreshAsync();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (IsVisible && WindowState != WindowState.Minimized) await RefreshAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async Task RefreshAsync()
        {
            if (_refreshing || _closed) return;
            _refreshing = true;
            try
            {
                var snapshots = await Task.Run(() => (_discovery.Inspect(DeveloperToolKind.Python), _discovery.Inspect(DeveloperToolKind.NodeJs)), _lifetime.Token);
                if (_closed) return;
                Python.Apply(snapshots.Item1);
                NodeJs.Apply(snapshots.Item2);
                LastChecked.Text = $"最近检测：{DateTime.Now:HH:mm:ss} · 每 15 秒自动刷新";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!_closed) OperationStatus.Text = "检测失败：" + ex.Message; }
            finally { _refreshing = false; }
        }

        private async void LoadVersions_Click(object sender, RoutedEventArgs e)
        {
            if (_operationBusy || _closed || (sender as FrameworkElement)?.DataContext is not DeveloperToolPageModel page) return;
            SetBusy(true);
            page.CatalogStatus = "正在从官网获取稳定版本…";
            try
            {
                var releases = await _catalog.GetReleasesAsync(page.Kind, _lifetime.Token);
                if (_closed) return;
                var previousVersion = page.SelectedRelease?.Version;
                page.Releases.Clear();
                foreach (var release in releases) page.Releases.Add(release);
                page.SelectedRelease = page.Releases.FirstOrDefault(release => release.Version == previousVersion) ?? page.Releases.FirstOrDefault();
                page.CatalogStatus = releases.Count == 0 ? "官网没有返回稳定版本，请稍后重试。" : "已获取 Windows x64 稳定版本。";
                OperationStatus.Text = $"已更新 {page.Title} 可安装版本。尚未下载或安装。";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_closed) page.CatalogStatus = "获取版本失败：" + ex.Message + "。不会改用未经核验的版本目录。";
            }
            finally { SetBusy(false); }
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (_operationBusy || _closed || (sender as FrameworkElement)?.DataContext is not DeveloperToolPageModel page
                || page.SelectedRelease is not DeveloperToolRelease release) return;
            var download = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (download == null)
            {
                OperationStatus.Text = "下载服务不可用，未开始安装。";
                return;
            }
            SetBusy(true);
            try
            {
                CancellationToken cancellationToken = _lifetime.Token;
                DeveloperToolDownloadSource source = page.SelectedSourceIndex == 1 ? DeveloperToolDownloadSource.Official : DeveloperToolDownloadSource.DomesticMirror;
                OperationStatus.Text = $"正在获取 {release.DisplayName} 的官网 SHA256…";
                string expectedHash = await _catalog.GetOfficialSha256Async(release, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                string directory = Path.Combine(Environments.DirToolPackageCache, "DeveloperTools", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                OperationStatus.Text = $"正在下载 {release.FileName}，进度可在下载管理器查看。关闭本窗口会取消后续自动安装。";
                download.ShowDownloadWindow();
                // Public mirrors must never receive the application's backend credentials.
                download.Download(release.GetDownloadUri(source).AbsoluteUri, directory, authorization: null,
                    onCompleted: path => completion.TrySetResult(path));
                string? filePath = await completion.Task.WaitAsync(TimeSpan.FromMinutes(30), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(filePath)) throw new IOException("下载未完成，请在下载管理器检查错误，或切换官方网站重试。");
                string expectedPath = Path.GetFullPath(Path.Combine(directory, release.FileName));
                if (!string.Equals(Path.GetFullPath(filePath), expectedPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("下载服务返回了意外的文件路径，已阻止安装。");
                OperationStatus.Text = "正在校验 SHA256 和发布者数字签名…";
                using var verified = await Task.Run(() => DeveloperToolInstallerService.PrepareInstaller(expectedPath, release, expectedHash), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                OperationStatus.Text = "校验通过，正在启动官方安装向导。安装位置和系统更改由向导确认。";
                // Verification runs off the UI thread. The file remains locked until the installer exits.
                // Launch happens on the UI thread after the lifetime check; closing never kills an installer.
                using Process installer = verified.Start();
                await installer.WaitForExitAsync(cancellationToken);
                await RefreshAsync();
                if (_closed) return;
                bool detected = page.Installations.Any(item => string.Equals(item.Version, release.Version.ToString(), StringComparison.Ordinal));
                OperationStatus.Text = installer.ExitCode == 3010
                    ? "安装向导提示需要重启 Windows。请保存工作后自行重启，并重新检测。"
                    : $"安装向导已退出（代码 {installer.ExitCode}）。{(detected ? "已检测到所选版本。" : "尚未检测到所选版本，请检查向导结果并刷新。")} 新终端或重启应用后再核对默认命令。";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_closed) OperationStatus.Text = "安装未完成：" + ex.Message;
            }
            finally { SetBusy(false); }
        }

        private void SetBusy(bool value)
        {
            _operationBusy = value;
            if (!_closed) ToolTabs.IsEnabled = !value;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e)
        {
            _closed = true;
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
