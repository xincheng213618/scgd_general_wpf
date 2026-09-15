using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ToolPlugins.CameraDriver
{
    internal static class CameraDriverText
    {
        internal static string Get(string key) => Properties.Resources.ResourceManager.GetString("CameraDriverWizard" + key, Properties.Resources.Culture) ?? key;
        internal static string Describe(CameraDriverInstallationStatus? status) => status == null ? Get("Checking")
            : status.IsInstalled ? string.Format(Get("InstalledFormat"), status.RegisteredVersion)
            : status.HasPartialInstallation ? Get("Incomplete") : Get("Missing");
    }

    internal interface ICameraDriverPackageDownloader
    {
        Task<string> DownloadAsync(CancellationToken cancellationToken);
    }

    internal sealed class CameraDriverPackageDownloader(Func<IDownloadService> resolveService, string cacheDirectory,
        Func<string?> getAuthorization) : ICameraDriverPackageDownloader
    {
        public async Task<string> DownloadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDownloadService service = resolveService();
            string directory = Path.Combine(cacheDirectory, "CameraDriver", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            service.ShowDownloadWindow();
            service.Download(CameraDriverInstallationService.DownloadUrl, directory, getAuthorization(), path => completion.TrySetResult(path));
            string? downloadedPath = await completion.Task.WaitAsync(TimeSpan.FromMinutes(30), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(downloadedPath)) throw new IOException(CameraDriverText.Get("DownloadFailed"));
            string expectedPath = Path.GetFullPath(Path.Combine(directory, CameraDriverInstallationService.InstallerFileName));
            if (!string.Equals(Path.GetFullPath(downloadedPath), expectedPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(CameraDriverText.Get("UnexpectedPath"));
            return expectedPath;
        }
    }

    internal sealed class CameraDriverWindowModel(ICameraDriverInstallationService installationService,
        ICameraDriverPackageDownloader downloader) : ViewModelBase
    {
        private CameraDriverInstallationStatus? _status;
        private bool _isBusy;
        private bool _restartRequired;
        private string _operationMessage = string.Empty;

        public string Title => CameraDriverText.Get("Header");
        public string Hint => CameraDriverText.Get("InstallHint");
        public string RefreshText => CameraDriverText.Get("RecheckAction");
        public string DownloadPageText => CameraDriverText.Get("DownloadPage");
        public string CloseText => Properties.Resources.ResourceManager.GetString("Close", Properties.Resources.Culture) ?? "Close";
        public string StatusSummary => _status == null && !IsBusy ? CameraDriverText.Get("Unknown") : CameraDriverText.Describe(_status);
        public string VersionDetails => _status == null ? string.Empty : string.Format(CameraDriverText.Get("VersionDetailsFormat"),
            _status.RegisteredVersion?.ToString() ?? "—", _status.FirmwareVersion?.ToString() ?? "—", _status.IoVersion?.ToString() ?? "—");
        public string InstallText => CameraDriverText.Get(_status?.IsInstalled == true ? "ReinstallAction" : "InstallAction");
        public bool IsBusy => _isBusy;
        public bool CanRefresh => !IsBusy;
        public bool CanInstall => !IsBusy && !_restartRequired;
        public string OperationMessage => _operationMessage;

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            if (IsBusy) return;
            _isBusy = true;
            _operationMessage = string.Empty;
            _status = null;
            NotifyStateChanged();
            try
            {
                _status = await installationService.QueryAsync(cancellationToken);
                if (_restartRequired) _operationMessage = CameraDriverText.Get("RestartRequired");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex) { _operationMessage = string.Format(CameraDriverText.Get("ErrorFormat"), ex.Message); }
            finally { _isBusy = false; NotifyStateChanged(); }
        }

        public async Task InstallAsync(CancellationToken cancellationToken)
        {
            if (!CanInstall) return;
            _isBusy = true;
            _operationMessage = CameraDriverText.Get("Downloading");
            NotifyStateChanged();
            try
            {
                // Both install and reinstall use the download manager, even when detection says installed.
                string path = await downloader.DownloadAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _operationMessage = CameraDriverText.Get("Working");
                NotifyStateChanged();
                int exitCode = await installationService.InstallAsync(path, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _restartRequired = exitCode == 3010;
                _status = null;
                _status = await installationService.QueryAsync(cancellationToken);
                _operationMessage = _restartRequired ? CameraDriverText.Get("RestartRequired")
                    : exitCode != 0 ? string.Format(CameraDriverText.Get("ExitCodeFormat"), exitCode)
                    : _status.IsInstalled ? CameraDriverText.Get("InstalledResult") : CameraDriverText.Get("InstallIncomplete");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { _operationMessage = CameraDriverText.Get("Cancelled"); }
            catch (Exception ex) { _operationMessage = string.Format(CameraDriverText.Get("ErrorFormat"), ex.Message); }
            finally { _isBusy = false; NotifyStateChanged(); }
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(StatusSummary));
            OnPropertyChanged(nameof(VersionDetails));
            OnPropertyChanged(nameof(InstallText));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(OperationMessage));
        }
    }
}
