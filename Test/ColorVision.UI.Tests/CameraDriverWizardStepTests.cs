using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Wizards;
using ColorVision.ToolPlugins.CameraDriver;
using ColorVision.UI;
using System.ComponentModel;
using System.IO;


namespace ColorVision.UI.Tests;

public sealed class CameraDriverWizardStepTests
{
    private const string RegisteredInf = """
        [Version]
        ClassGUID={94F18CEC-CA64-40F6-9A81-17A67720ABF0}
        Provider=%ColorVision_Provider%
        DriverVer=03/01/2024,24.03.01.0
        [ColorVision_1ST.AddService]
        ServiceBinary=%12%\ColorVision_FW.sys
        [ColorVision_2ND.AddService]
        ServiceBinary=%12%\ColorVision_IO.sys
        [Strings]
        ColorVision_Provider="ColorVision"
        """;

    [Fact]
    public void Detection_RequiresBothSystemBinariesAndRegisteredInfButNotUninstallEntry()
    {
        Version version = new(24, 3, 1, 0);
        Assert.False(new CameraDriverInstallationStatus(null, null, null, true).IsInstalled);
        Assert.False(new CameraDriverInstallationStatus(version, version, null, true).IsInstalled);
        Assert.False(new CameraDriverInstallationStatus(null, version, version, true).IsInstalled);
        Assert.True(new CameraDriverInstallationStatus(version, version, new(1, 2, 3, 20), false).IsInstalled);
    }

    [Fact]
    public void Detection_ReadsInfVersionAndIgnoresCommentsAndUnrelatedDrivers()
    {
        Assert.Equal(new Version(24, 3, 1, 0), CameraDriverInstallationService.ReadRegisteredVersion(RegisteredInf));
        Assert.Equal(new Version(24, 3, 1, 0), CameraDriverInstallationService.ReadRegisteredVersion(
            RegisteredInf.Replace("DriverVer=", " ; DriverVer=01/01/2099,99.1.1.0\nDriverVer=")));
        Assert.Null(CameraDriverInstallationService.ReadRegisteredVersion(RegisteredInf.Replace("\"ColorVision\"", "\"Other vendor\"")));
        Assert.Null(CameraDriverInstallationService.ReadRegisteredVersion(RegisteredInf.Replace("ColorVision_IO.sys", "Other_IO.sys")));
        Assert.Null(CameraDriverInstallationService.ReadRegisteredVersion(RegisteredInf.Replace("24.03.01.0", "invalid")));
        Assert.Null(CameraDriverInstallationService.ReadRegisteredVersion("; " + RegisteredInf.Replace("\n", "\n; ")));
    }

    [Fact]
    public async Task WizardSkipsWithoutOpeningWindowAndRechecksAfterManagementCloses()
    {
        FakeService service = new();
        int opened = 0;
        CameraDriverWizardStep step = new(service, () => { opened++; service.Status = Installed; });
        await step.RefreshAsync();
        Assert.False(step.IsRequired);
        Assert.True(step.RunsBeforeInitializers);
        Assert.False(step.ConfigurationStatus);
        Assert.True(await step.ApplyAsync());
        Assert.True(step.ConfigurationStatus);
        Assert.Equal(0, opened);
        await step.ExecuteAsync();
        Assert.Equal(1, opened);
        Assert.True(step.ConfigurationStatus);
        Assert.Equal(0, service.InstallCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallAndReinstallBothDownloadAndRecheck(bool alreadyInstalled)
    {
        FakeService service = new() { Status = alreadyInstalled ? Installed : Missing };
        FakeDownloader downloader = new();
        CameraDriverWindowModel model = new(service, downloader);
        await model.RefreshAsync(CancellationToken.None);
        Assert.Equal(CameraDriverText.Get(alreadyInstalled ? "ReinstallAction" : "InstallAction"), model.InstallText);
        service.Install = () => { service.Status = Installed; return Task.FromResult(0); };
        await model.InstallAsync(CancellationToken.None);
        Assert.Equal(1, downloader.Calls);
        Assert.Equal(1, service.InstallCalls);
        Assert.Equal("downloaded-driver.exe", service.InstalledPath);
        Assert.Equal(CameraDriverText.Get("InstalledResult"), model.OperationMessage);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task InstallerExitZeroWithoutDriverFilesIsNotReportedAsSuccess()
    {
        CameraDriverWindowModel model = new(new FakeService(), new FakeDownloader());
        await model.InstallAsync(CancellationToken.None);
        Assert.Equal(CameraDriverText.Get("InstallIncomplete"), model.OperationMessage);
        Assert.True(model.CanInstall);
    }

    [Fact]
    public async Task ClosingWhileDownloadingPreventsLateCompletionFromStartingInstaller()
    {
        TaskCompletionSource<string> completion = new();
        FakeService service = new();
        FakeDownloader downloader = new() { Download = _ => completion.Task };
        CameraDriverWindowModel model = new(service, downloader);
        using CancellationTokenSource lifetime = new();
        Task operation = model.InstallAsync(lifetime.Token);
        Assert.True(model.IsBusy);
        await model.InstallAsync(lifetime.Token);
        Assert.Equal(1, downloader.Calls);
        lifetime.Cancel();
        completion.SetResult("downloaded-driver.exe");
        await operation;
        Assert.Equal(0, service.InstallCalls);
        Assert.False(model.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledInstallerRemainsRetryable(bool cancelElevation)
    {
        FakeService service = new()
        {
            Install = () => cancelElevation ? Task.FromException<int>(new Win32Exception(1223)) : Task.FromResult(2)
        };
        CameraDriverWindowModel model = new(service, new FakeDownloader());
        await model.InstallAsync(CancellationToken.None);
        Assert.Equal(cancelElevation ? CameraDriverText.Get("Cancelled") : string.Format(CameraDriverText.Get("ExitCodeFormat"), 2), model.OperationMessage);
        Assert.True(model.CanInstall);
    }

    [Fact]
    public async Task RestartRequiredPreventsAnotherInstall()
    {
        FakeService service = new() { Install = () => Task.FromResult(3010) };
        FakeDownloader downloader = new();
        CameraDriverWindowModel model = new(service, downloader);
        await model.InstallAsync(CancellationToken.None);
        await model.InstallAsync(CancellationToken.None);
        Assert.Equal(1, downloader.Calls);
        Assert.False(model.CanInstall);
        Assert.Equal(CameraDriverText.Get("RestartRequired"), model.OperationMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadAdapterUsesExistingManagerAndRejectsUnexpectedPath(bool wrongPath)
    {
        string cache = Directory.CreateTempSubdirectory("CameraDriverDownloadTests-").FullName;
        try
        {
            RecordingDownloadService service = new() { WrongPath = wrongPath };
            CameraDriverPackageDownloader downloader = new(() => service, cache, () => null);
            if (wrongPath) await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(CancellationToken.None));
            else
            {
                string path = await downloader.DownloadAsync(CancellationToken.None);
                Assert.Equal(Path.Combine(service.Directory!, CameraDriverInstallationService.InstallerFileName), path);
            }
            Assert.Equal(CameraDriverInstallationService.DownloadUrl, service.Url);
            Assert.Equal(1, service.Shown);
        }
        finally
        {
            // Only empty directories created by this fixture; the fake downloader creates no files.
            foreach (string directory in Directory.GetDirectories(Path.Combine(cache, "CameraDriver"))) Directory.Delete(directory);
            Directory.Delete(Path.Combine(cache, "CameraDriver"));
            Directory.Delete(cache);
        }
    }

    [Fact]
    public async Task FailedDownloadNeverStartsAnInstaller()
    {
        FakeService service = new();
        CameraDriverWindowModel model = new(service, new FakeDownloader { Download = _ => Task.FromException<string>(new IOException("download failed")) });
        await model.InstallAsync(CancellationToken.None);
        Assert.Equal(0, service.InstallCalls);
        Assert.Contains("download failed", model.OperationMessage);
        Assert.True(model.CanInstall);
    }

    [Fact]
    public void WindowBindsAndLaysOutWithoutStartingDownloadsOrInstallers()
    {
        WpfTestHost.Invoke(() =>
        {
            FakeService service = new() { Status = Installed };
            FakeDownloader downloader = new();
            CameraDriverWindowModel model = new(service, downloader);
            model.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            CameraDriverWindow window = new(model);
            try
            {
                var content = (System.Windows.FrameworkElement)window.Content;
                window.Content = null;
                var surface = new System.Windows.Controls.Border { DataContext = model, Child = content };
                surface.Measure(new System.Windows.Size(680, 430));
                surface.Arrange(new System.Windows.Rect(0, 0, 680, 430));
                surface.UpdateLayout();
                Assert.Equal(CameraDriverText.Get("ReinstallAction"), model.InstallText);
                Assert.Equal(0, downloader.Calls);
                Assert.Equal(0, service.InstallCalls);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task DownloadedHtmlOrCorruptFileIsRejectedBeforeLaunching()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, "<html>download error</html>");
            await Assert.ThrowsAsync<InvalidDataException>(() => new CameraDriverInstallationService().InstallAsync(file, CancellationToken.None));
        }
        finally { File.Delete(file); }
    }

    private static CameraDriverInstallationStatus Missing => new(null, null, null, false);
    private static CameraDriverInstallationStatus Installed => new(new(24, 3, 1, 0), new(24, 3, 1, 0), new(1, 2, 3, 20), true);

    private sealed class FakeService : ICameraDriverInstallationService
    {
        public CameraDriverInstallationStatus Status { get; set; } = Missing;
        public Func<Task<int>> Install { get; set; } = () => Task.FromResult(0);
        public int InstallCalls { get; private set; }
        public string? InstalledPath { get; private set; }
        public Task<CameraDriverInstallationStatus> QueryAsync(CancellationToken cancellationToken) => Task.FromResult(Status);
        public Task<int> InstallAsync(string path, CancellationToken cancellationToken) { InstallCalls++; InstalledPath = path; return Install(); }
    }

    private sealed class FakeDownloader : ICameraDriverPackageDownloader
    {
        public int Calls { get; private set; }
        public Func<CancellationToken, Task<string>> Download { get; set; } = _ => Task.FromResult("downloaded-driver.exe");
        public Task<string> DownloadAsync(CancellationToken cancellationToken) { Calls++; return Download(cancellationToken); }
    }

    private sealed class RecordingDownloadService : IDownloadService
    {
        public string? Url { get; private set; }
        public string? Directory { get; private set; }
        public int Shown { get; private set; }
        public bool WrongPath { get; init; }
        public void ShowDownloadWindow() => Shown++;
        public void CloseDownloadWindow() { }
        public void Download(string url, string saveDir, string? authorization = null, Action<string?>? onCompleted = null)
        {
            Url = url;
            Directory = saveDir;
            onCompleted?.Invoke(Path.Combine(saveDir, WrongPath ? "other.exe" : CameraDriverInstallationService.InstallerFileName));
        }
    }
}
