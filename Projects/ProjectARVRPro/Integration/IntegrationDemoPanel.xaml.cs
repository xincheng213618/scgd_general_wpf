using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Marketplace;
using log4net;
using ProjectARVRPro.PluginConfig;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

#pragma warning disable CA1001 // CancellationTokenSource follows the WPF Loaded/Unloaded lifecycle.

namespace ProjectARVRPro.Integration;

public partial class IntegrationDemoPanel : UserControl
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(IntegrationDemoPanel));
    private readonly IntegrationDemoReleaseClient _releaseClient = new();
    private readonly string _downloadDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "ColorVision",
        "ProjectARVRPro.IntegrationDemo");
    private CancellationTokenSource? _metadataCancellation;
    private IntegrationDemoReleaseInfo? _latestRelease;
    private bool _metadataLoaded;
    private bool _isLoading;
    private bool _isDownloading;

    public IntegrationDemoPanel()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Version? version = typeof(ProjectARVRLitePlugin).Assembly.GetName().Version;
        CurrentPluginVersionText.Text = version?.ToString() ?? "未知";
        OutputModeText.Text = ViewResultManager.GetInstance().Config.UseLegacyARVROutput ? "Legacy 扁平结果" : "标准嵌套结果";
        ServiceUrlText.Text = MarketplaceConfig.ServiceBaseUrl;
        RunAllCommandTextBox.Text = "ProjectARVRPro.IntegrationDemo.exe --host <ColorVision-IP> --port 6666 --sn SN001 --mode runall";
        ParseCommandTextBox.Text = "ProjectARVRPro.IntegrationDemo.exe --parse-file Samples\\project-arvr-result.json";
        if (IsVisible)
            _ = RefreshReleaseAsync(force: false);
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _metadataCancellation?.Cancel();
        _metadataCancellation?.Dispose();
        _metadataCancellation = null;
    }

    private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded && IsVisible)
            _ = RefreshReleaseAsync(force: false);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshReleaseAsync(force: true);
    }

    private async Task RefreshReleaseAsync(bool force)
    {
        if (_isLoading || (!force && _metadataLoaded))
            return;

        _metadataCancellation?.Cancel();
        _metadataCancellation?.Dispose();
        _metadataCancellation = new CancellationTokenSource();
        _isLoading = true;
        RefreshButton.IsEnabled = false;
        DownloadButton.IsEnabled = false;
        CopyDownloadLinkButton.IsEnabled = false;
        StatusText.Text = "正在检查 Demo 最新版本…";

        try
        {
            IntegrationDemoReleaseInfo release = await _releaseClient.GetLatestAsync(_metadataCancellation.Token);
            _latestRelease = release;
            _metadataLoaded = true;
            LatestVersionText.Text = release.Version;
            VerifiedPluginVersionText.Text = string.IsNullOrWhiteSpace(release.VerifiedProjectARVRProVersion) ? "未注明" : release.VerifiedProjectARVRProVersion;
            ProtocolVersionText.Text = release.ProtocolVersion;
            RuntimeText.Text = string.IsNullOrWhiteSpace(release.RequiresDotNetFramework) ? ".NET Framework 4.8" : ".NET Framework " + release.RequiresDotNetFramework;
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(release.ReleaseNotes) ? "本版本未提供更新说明。" : release.ReleaseNotes;
            StatusText.Text = $"可下载 {release.FileName}（{FormatBytes(release.SizeBytes)}），下载后将自动校验 SHA-256。";
            DownloadButton.IsEnabled = !_isDownloading;
            CopyDownloadLinkButton.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _latestRelease = null;
            _metadataLoaded = false;
            LatestVersionText.Text = "暂不可用";
            VerifiedPluginVersionText.Text = "—";
            ProtocolVersionText.Text = "—";
            ReleaseNotesText.Text = "—";
            StatusText.Text = "未能获取 Demo 发布信息。可能尚未发布，或当前无法连接下载服务。";
            Log.Warn("获取 ProjectARVRPro IntegrationDemo 发布信息失败。", ex);
        }
        finally
        {
            _isLoading = false;
            RefreshButton.IsEnabled = true;
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
            return;
        if (_latestRelease == null)
        {
            await RefreshReleaseAsync(force: true);
            if (_latestRelease == null)
                return;
        }

        IntegrationDemoReleaseInfo release = _latestRelease;
        string downloadUrl = _releaseClient.GetDownloadUrl(release);
        IDownloadService? downloadService = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
        if (downloadService == null)
        {
            StatusText.Text = "下载服务不可用，请复制下载链接后在浏览器中打开。";
            return;
        }

        _isDownloading = true;
        DownloadButton.IsEnabled = false;
        StatusText.Text = $"正在下载 {release.FileName}…";
        try
        {
            Directory.CreateDirectory(_downloadDirectory);
            downloadService.ShowDownloadWindow();
            downloadService.Download(downloadUrl, _downloadDirectory, DownloadFileConfig.Instance.Authorization, filePath =>
            {
                _ = Dispatcher.InvokeAsync(async () => await HandleDownloadCompletedAsync(filePath, release));
            });
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            DownloadButton.IsEnabled = true;
            StatusText.Text = "启动 Demo 下载失败：" + ex.Message;
            Log.Error("启动 ProjectARVRPro IntegrationDemo 下载失败。", ex);
        }
    }

    private async Task HandleDownloadCompletedAsync(string? filePath, IntegrationDemoReleaseInfo release)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusText.Text = "Demo 下载失败，请检查下载窗口和网络状态后重试。";
                return;
            }

            StatusText.Text = "下载完成，正在校验文件…";
            (bool IsValid, string Error) verification = await Task.Run(() =>
            {
                bool isValid = IntegrationDemoReleaseClient.VerifyPackage(filePath, release, out string error);
                return (isValid, error);
            });
            if (!verification.IsValid)
            {
                TryDeleteInvalidDownload(filePath);
                StatusText.Text = "Demo 文件校验失败：" + verification.Error;
                return;
            }

            StatusText.Text = $"Demo {release.Version} 下载并校验完成：{filePath}";
            PlatformHelper.OpenFolder(Path.GetDirectoryName(filePath));
        }
        catch (Exception ex)
        {
            StatusText.Text = "处理 Demo 下载文件失败：" + ex.Message;
            Log.Error("处理 ProjectARVRPro IntegrationDemo 下载文件失败。", ex);
        }
        finally
        {
            _isDownloading = false;
            DownloadButton.IsEnabled = _latestRelease != null;
        }
    }

    private void CopyDownloadLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestRelease == null)
            return;

        Clipboard.SetText(_releaseClient.GetDownloadUrl(_latestRelease));
        StatusText.Text = "Demo 下载链接已复制。";
    }

    private void OpenDownloadDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_downloadDirectory);
        PlatformHelper.OpenFolder(_downloadDirectory);
    }

    private void CopyCommandButton_Click(object sender, RoutedEventArgs e)
    {
        string text = (sender as Button)?.Tag as string == "Parse" ? ParseCommandTextBox.Text : RunAllCommandTextBox.Text;
        Clipboard.SetText(text);
        StatusText.Text = "命令已复制。";
    }

    private static void TryDeleteInvalidDownload(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            Log.Warn($"删除校验失败的 Demo 下载文件失败：{filePath}", ex);
        }
    }

    private static string FormatBytes(long sizeBytes)
    {
        if (sizeBytes >= 1024L * 1024L)
            return $"{sizeBytes / (1024d * 1024d):0.##} MB";
        if (sizeBytes >= 1024L)
            return $"{sizeBytes / 1024d:0.##} KB";
        return $"{sizeBytes} B";
    }
}
