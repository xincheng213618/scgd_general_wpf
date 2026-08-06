using ColorVision.UI.Menus;
using log4net;
using Spectrum.Menus;
using System.Globalization;
using System.Windows;

namespace Spectrum.Update;

public sealed class MenuSpectrumUpdate : SpectrumMenuIBase
{
    public override string OwnerGuid => MenuItemConstants.Help;
    public override int Order => 5;
    public override string Header => UpdateText.Get("CheckForUpdates", "检查更新");
    public override Visibility Visibility => SpectrumRuntime.IsStandalone ? Visibility.Visible : Visibility.Collapsed;

    public override void Execute() => SpectrumUpdateCoordinator.ShowManualCheck();
}

internal static class SpectrumUpdateCoordinator
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(SpectrumUpdateCoordinator));
    private static int startupCheckStarted;
    private static SpectrumUpdateWindow? updateWindow;

    public static async Task CheckAtStartupAsync(Window owner)
    {
        if (!SpectrumRuntime.IsStandalone || Interlocked.Exchange(ref startupCheckStarted, 1) != 0)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
            SpectrumUpdateCheckResult result = await SpectrumUpdateService.CheckLatestAsync(timeout.Token);
            if (!result.IsUpdateAvailable || !owner.IsLoaded)
            {
                return;
            }

            await owner.Dispatcher.InvokeAsync(() => ShowWindow(owner, result));
        }
        catch (OperationCanceledException)
        {
            Log.Info("Spectrum 启动更新检查超时，已跳过");
        }
        catch (Exception ex)
        {
            Log.Warn("Spectrum 启动更新检查失败，已跳过", ex);
        }
    }

    public static void ShowManualCheck()
    {
        if (!SpectrumRuntime.IsStandalone)
        {
            return;
        }

        Window? owner = MainWindow.Instance ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        if (owner != null)
        {
            ShowWindow(owner, null);
        }
    }

    private static void ShowWindow(Window owner, SpectrumUpdateCheckResult? result)
    {
        if (updateWindow is { IsLoaded: true })
        {
            updateWindow.Activate();
            return;
        }

        updateWindow = new SpectrumUpdateWindow(result)
        {
            Owner = owner
        };
        updateWindow.Closed += (_, _) => updateWindow = null;
        updateWindow.Show();
    }
}

public partial class SpectrumUpdateWindow : Window, IDisposable
{
    private readonly ILog log = LogManager.GetLogger(typeof(SpectrumUpdateWindow));
    private SpectrumUpdateCheckResult? checkResult;
    private SpectrumDownloadedUpdate? downloadedUpdate;
    private CancellationTokenSource? operationCancellation;
    private bool isChecking;
    private bool isDownloading;
    private bool installerLaunched;

    internal SpectrumUpdateWindow(SpectrumUpdateCheckResult? initialResult)
    {
        checkResult = initialResult;
        InitializeComponent();
        ApplyText();
        Loaded += SpectrumUpdateWindow_Loaded;
        Closing += SpectrumUpdateWindow_Closing;
    }

    private void ApplyText()
    {
        Title = UpdateText.Get("SpectrumUpdateTitle", "Spectrum 更新");
        HeadingText.Text = UpdateText.Get("SpectrumUpdateHeading", "Spectrum 软件更新");
        CurrentVersionLabel.Text = UpdateText.Get("CurrentVersion", "当前版本");
        LatestVersionLabel.Text = UpdateText.Get("LatestVersion", "最新版本");
        ReleaseNotesGroup.Header = UpdateText.Get("ReleaseNotes", "更新说明");
        CurrentVersionText.Text = SpectrumRuntime.CurrentVersion.ToString();
        LatestVersionText.Text = "--";
        ReleaseNotesText.Text = UpdateText.Get("CheckingForUpdates", "正在检查更新...");
        StatusText.Text = UpdateText.Get("CheckingForUpdates", "正在检查更新...");
        CloseButton.Content = UpdateText.Get("Close", "关闭");
        CancelDownloadButton.Content = UpdateText.Get("CancelDownload", "取消下载");
        PrimaryButton.Content = UpdateText.Get("CheckAgain", "重新检查");
    }

    private async void SpectrumUpdateWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SpectrumUpdateWindow_Loaded;
        if (checkResult == null)
        {
            await CheckAsync();
        }
        else
        {
            ShowAvailableUpdate(checkResult);
        }
    }

    private async Task CheckAsync()
    {
        if (isChecking || isDownloading)
        {
            return;
        }

        isChecking = true;
        PrimaryButton.IsEnabled = false;
        LatestVersionText.Text = "--";
        ReleaseNotesText.Text = string.Empty;
        StatusText.Text = UpdateText.Get("CheckingForUpdates", "正在检查更新...");
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            checkResult = await SpectrumUpdateService.CheckLatestAsync(timeout.Token);
            LatestVersionText.Text = checkResult.Version.ToString();
            if (checkResult.IsUpdateAvailable)
            {
                ShowAvailableUpdate(checkResult);
            }
            else
            {
                ReleaseNotesText.Text = checkResult.Manifest.ReleaseNotes;
                StatusText.Text = UpdateText.Get("AlreadyUpToDate", "当前已是最新版本。");
                PrimaryButton.Content = UpdateText.Get("CheckAgain", "重新检查");
                PrimaryButton.IsEnabled = true;
            }
        }
        catch (OperationCanceledException)
        {
            ShowCheckError(UpdateText.Get("UpdateCheckTimeout", "检查更新超时，请稍后重试。"));
        }
        catch (Exception ex)
        {
            log.Warn("手动检查 Spectrum 更新失败", ex);
            ShowCheckError(ex.Message);
        }
        finally
        {
            isChecking = false;
        }
    }

    private void ShowCheckError(string message)
    {
        StatusText.Text = message;
        PrimaryButton.Content = UpdateText.Get("CheckAgain", "重新检查");
        PrimaryButton.IsEnabled = true;
    }

    private void ShowAvailableUpdate(SpectrumUpdateCheckResult result)
    {
        LatestVersionText.Text = result.Version.ToString();
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(result.Manifest.ReleaseNotes)
            ? UpdateText.Get("NoReleaseNotes", "此版本没有附加说明。")
            : result.Manifest.ReleaseNotes;
        StatusText.Text = UpdateText.Get("NewVersionAvailable", "发现新版本。下载后会完成安全校验，再提示重启安装。");
        ProgressText.Text = FormatSize(result.Manifest.Package.Size);
        PrimaryButton.Content = UpdateText.Get("DownloadUpdate", "下载更新");
        PrimaryButton.IsEnabled = true;
    }

    private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (isChecking || isDownloading)
        {
            return;
        }

        if (downloadedUpdate != null)
        {
            InstallDownloadedUpdate();
            return;
        }

        if (checkResult?.IsUpdateAvailable == true)
        {
            await DownloadAsync(checkResult.Manifest);
            return;
        }

        await CheckAsync();
    }

    private async Task DownloadAsync(SpectrumUpdateManifest manifest)
    {
        isDownloading = true;
        operationCancellation = new CancellationTokenSource();
        PrimaryButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        CancelDownloadButton.Visibility = Visibility.Visible;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        StatusText.Text = UpdateText.Get("DownloadingUpdate", "正在下载完整更新包...");
        ProgressText.Text = $"0 / {FormatSize(manifest.Package.Size)}";

        Progress<SpectrumDownloadProgress> progress = new(value =>
        {
            DownloadProgress.Value = value.Percentage;
            ProgressText.Text = $"{FormatSize(value.BytesReceived)} / {FormatSize(value.TotalBytes)} ({value.Percentage:F0}%)";
        });

        try
        {
            downloadedUpdate = await SpectrumUpdateService.DownloadAndValidateAsync(manifest, progress, operationCancellation.Token);
            DownloadProgress.Value = 100;
            StatusText.Text = UpdateText.Get("UpdateReadyToInstall", "下载和完整性校验已完成。请保存工作后重启安装。");
            PrimaryButton.Content = UpdateText.Get("RestartAndInstall", "重启并安装");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = UpdateText.Get("UpdateDownloadCancelled", "下载已取消。");
            DownloadProgress.Visibility = Visibility.Collapsed;
            ProgressText.Text = string.Empty;
            PrimaryButton.Content = UpdateText.Get("DownloadUpdate", "下载更新");
        }
        catch (Exception ex)
        {
            log.Error("Spectrum 更新包下载或校验失败", ex);
            StatusText.Text = ex.Message;
            DownloadProgress.Visibility = Visibility.Collapsed;
            PrimaryButton.Content = UpdateText.Get("RetryDownload", "重新下载");
        }
        finally
        {
            operationCancellation?.Dispose();
            operationCancellation = null;
            isDownloading = false;
            PrimaryButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
        }
    }

    private void InstallDownloadedUpdate()
    {
        if (downloadedUpdate == null)
        {
            return;
        }

        MainWindow? mainWindow = MainWindow.Instance;
        if (mainWindow != null && !mainWindow.CanInstallUpdate(out string reason))
        {
            StatusText.Text = reason;
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            UpdateText.Get("RestartInstallConfirmation", "Spectrum 将退出并安装已校验的更新，然后自动重新启动。是否继续？"),
            UpdateText.Get("SpectrumUpdateTitle", "Spectrum 更新"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        if (mainWindow != null && !mainWindow.CanInstallUpdate(out reason))
        {
            StatusText.Text = reason;
            return;
        }

        if (!SpectrumUpdateService.TryLaunchInstaller(downloadedUpdate, out string? errorMessage))
        {
            StatusText.Text = errorMessage;
            return;
        }

        installerLaunched = true;
        Application.Current.Shutdown();
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e) => operationCancellation?.Cancel();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SpectrumUpdateWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (isDownloading)
        {
            operationCancellation?.Cancel();
            e.Cancel = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
        if (!installerLaunched && downloadedUpdate != null)
        {
            SpectrumUpdateService.DiscardDownloadedUpdate(downloadedUpdate);
            downloadedUpdate = null;
        }
        GC.SuppressFinalize(this);
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", value, units[unit]);
    }
}
