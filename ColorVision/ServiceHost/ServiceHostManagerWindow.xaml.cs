#pragma warning disable CA1863
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.UI.ServiceHost;
using ColorVision.UI.LogImp;
using ColorVision.Update.Export;
using log4net;
using AppResources = ColorVision.Properties.Resources;

namespace ColorVision.ServiceHost
{
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF window releases the log binder on Closed.")]
    public partial class ServiceHostManagerWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceHostManagerWindow));
        private static readonly string ServiceHostLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost",
            "ColorVisionServiceHost.log");

        private ServiceHostStatus? _lastStatus;
        private Com0ComStatusInfo? _com0ComStatus;
        private bool _isBusy;
        private readonly ModuleLogViewerBinder _logBinder;

        public ServiceHostManagerWindow()
        {
            InitializeComponent();
            _logBinder = new ModuleLogViewerBinder(LogViewer, "ColorVision.ServiceHost");
            InitializeStaticText();
            Loaded += async (_, _) => await RefreshStatusAsync().ConfigureAwait(true);
            Closed += (_, _) => _logBinder.Dispose();
        }

        private void InitializeStaticText()
        {
            ServiceNameText.Text = ServiceHostProtocol.ServiceName;
            PackagePathText.Text = ServiceHostProtocol.PackageExecutablePath;
            InstalledPathText.Text = ServiceHostProtocol.InstalledExecutablePath;
            LogPathText.Text = ServiceHostLogPath;
            SummaryText.Text = "Checking...";
            ActionHintText.Text = "Refresh";
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync().ConfigureAwait(true);
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            await RunOperationAsync("Install / Update", ColorVisionServiceHostManager.InstallAsync).ConfigureAwait(true);
        }

        private async void SelfUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await RunOperationAsync("Self Update", ColorVisionServiceHostManager.SelfUpdateAsync).ConfigureAwait(true);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await RunOperationAsync("Start", ColorVisionServiceHostManager.StartAsync).ConfigureAwait(true);
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await RunOperationAsync("Stop", ColorVisionServiceHostManager.StopAsync).ConfigureAwait(true);
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(this, "Uninstall ColorVisionServiceHost?", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            await RunOperationAsync("Uninstall", ColorVisionServiceHostManager.UninstallAsync).ConfigureAwait(true);
        }

        private async void PingButton_Click(object sender, RoutedEventArgs e)
        {
            await RunClientCommandAsync("Ping", token => ColorVisionServiceHostClient.Default.PingAsync(cancellationToken: token), useBusyState: false).ConfigureAwait(true);
        }

        private async void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            await RunClientCommandAsync("Status", token => ColorVisionServiceHostClient.Default.StatusAsync(cancellationToken: token), refreshAfter: true, useBusyState: false).ConfigureAwait(true);
        }

        private async void Com0ComRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            try
            {
                await RefreshCom0ComAsync(_lastStatus?.State == ServiceHostInstallState.Running).ConfigureAwait(true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Com0ComCreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Com0ComPortAComboBox.SelectedItem is not int portA
                || Com0ComPortBComboBox.SelectedItem is not int portB
                || portA == portB)
            {
                MessageBox.Show(this, "Select two different available port numbers.", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await RunCom0ComOperationAsync(
                "Create com0com Pair",
                token => ColorVisionServiceHostClient.Default.CreateCom0ComPairAsync(portA, portB, cancellationToken: token)).ConfigureAwait(true);
        }

        private void Com0ComPortComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateButtonAvailability();
        }

        private async void Com0ComDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Com0ComPairsGrid.SelectedItem is not Com0ComPairInfo pair)
                return;

            MessageBoxResult result = MessageBox.Show(
                this,
                $"Delete com0com pair {pair.DisplayName}?",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            await RunCom0ComOperationAsync(
                $"Delete com0com Pair {pair.PairNumber}",
                token => ColorVisionServiceHostClient.Default.DeleteCom0ComPairAsync(pair.PairNumber, cancellationToken: token)).ConfigureAwait(true);
        }

        private void Com0ComPairsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateButtonAvailability();
        }

        private async void WriteMarkerButton_Click(object sender, RoutedEventArgs e)
        {
            await RunClientCommandAsync("Write Marker", token => ColorVisionServiceHostClient.Default.SendAsync("write-demo-marker", TimeSpan.FromSeconds(5), token)).ConfigureAwait(true);
        }

        private async void FileAssociationButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            AppendLog("> File Association");
            try
            {
                bool success = await FileAssociationHelper.RegisterAssociationsAsync().ConfigureAwait(true);
                AppendLog(success ? "OK: file association registered" : "FAILED: file association registration failed");
                MessageBox.Show(this,
                    success ? AppResources.RegistryAppliedSuccess : AppResources.ComRegistrationFailed,
                    "ColorVision",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Error("File association registration failed.", ex);
                AppendLog(ex.ToString());
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await RefreshStatusAsync().ConfigureAwait(true);
                SetBusy(false);
            }
        }

        private async void RegisterThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            await RunThumbnailCommandAsync("register-thumbnail", "Register Thumbnail", AppResources.ThumbnailRegistrationSuccess, AppResources.RegistrationFailed).ConfigureAwait(true);
        }

        private async void UnregisterThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            await RunThumbnailCommandAsync("unregister-thumbnail", "Unregister Thumbnail", AppResources.ThumbnailUnregistered, AppResources.UnregistrationFailed).ConfigureAwait(true);
        }

        private void OpenPackageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenPath(ServiceHostProtocol.PackageExecutablePath, "Service host package executable was not found.");
        }

        private void OpenInstalledButton_Click(object sender, RoutedEventArgs e)
        {
            OpenPath(ServiceHostProtocol.InstalledExecutablePath, "Installed service host executable was not found.");
        }

        private void OpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            OpenPath(ServiceHostLogPath, "Service host log file was not found.");
        }

        private void CopyStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastStatus == null)
            {
                Clipboard.SetText("ColorVisionServiceHost status is not available.");
                AppendLog("Status copied: unavailable");
                return;
            }

            Clipboard.SetText(BuildStatusSnapshot(_lastStatus));
            AppendLog("Status copied to clipboard.");
        }

        private async Task RunOperationAsync(string name, Func<CancellationToken, Task<ServiceHostOperationResult>> operation)
        {
            SetBusy(true);
            AppendLog($"> {name}");
            try
            {
                ServiceHostOperationResult result = await operation(CancellationToken.None).ConfigureAwait(true);
                AppendLog(result.Summary);
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
            finally
            {
                await RefreshStatusAsync().ConfigureAwait(true);
                SetBusy(false);
            }
        }

        private async Task RunClientCommandAsync(
            string name,
            Func<CancellationToken, Task<ServiceHostResponse>> operation,
            bool refreshAfter = false,
            bool useBusyState = true)
        {
            if (useBusyState)
                SetBusy(true);
            AppendLog($"> {name}");
            try
            {
                ServiceHostResponse response = await operation(CancellationToken.None).ConfigureAwait(true);
                AppendLog(response.ToDisplayText());
            }
            catch (Exception ex)
            {
                AppendLog(ex.Message);
            }
            finally
            {
                if (refreshAfter)
                    await RefreshStatusAsync().ConfigureAwait(true);
                if (useBusyState)
                    SetBusy(false);
            }
        }

        private async Task RunCom0ComOperationAsync(
            string name,
            Func<CancellationToken, Task<ServiceHostResponse>> operation)
        {
            SetBusy(true);
            AppendLog($"> {name}");
            try
            {
                ServiceHostResponse response = await operation(CancellationToken.None).ConfigureAwait(true);
                AppendLog(response.ToDisplayText());
                MessageBox.Show(
                    this,
                    response.Success ? response.Message : response.ToDisplayText(),
                    "ColorVision",
                    MessageBoxButton.OK,
                    response.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Error($"{name} failed.", ex);
                AppendLog(ex.ToString());
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await RefreshCom0ComAsync(_lastStatus?.State == ServiceHostInstallState.Running).ConfigureAwait(true);
                SetBusy(false);
            }
        }

        private async Task RunThumbnailCommandAsync(string command, string label, string successMessage, string failureFormat)
        {
            SetBusy(true);
            AppendLog($"> {label}");
            try
            {
                string appPath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to resolve executable path.");
                string appDirectory = Path.GetDirectoryName(appPath) ?? throw new InvalidOperationException("Unable to resolve executable directory.");
                string comHostDll = Path.Combine(appDirectory, "ColorVision.ShellExtension.comhost.dll");

                if (!File.Exists(comHostDll))
                {
                    string message = string.Format(AppResources.ShellExtensionNotFound, comHostDll);
                    AppendLog(message);
                    MessageBox.Show(this, message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string thumbnailCacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "Windows",
                    "Explorer");

                ServiceHostResponse response = command switch
                {
                    "register-thumbnail" => await ColorVisionServiceHostClient.Default
                        .RegisterThumbnailAsync(appDirectory, thumbnailCacheDirectory)
                        .ConfigureAwait(true),
                    "unregister-thumbnail" => await ColorVisionServiceHostClient.Default
                        .UnregisterThumbnailAsync(appDirectory, thumbnailCacheDirectory)
                        .ConfigureAwait(true),
                    _ => await ColorVisionServiceHostClient.Default
                        .SendAsync(command, new { appDirectory, thumbnailCacheDirectory }, TimeSpan.FromSeconds(45))
                        .ConfigureAwait(true),
                };

                AppendLog(response.ToDisplayText());
                MessageBox.Show(this,
                    response.Success ? successMessage : string.Format(failureFormat, response.Message),
                    "ColorVision",
                    MessageBoxButton.OK,
                    response.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Error($"{command} failed.", ex);
                AppendLog(ex.ToString());
                MessageBox.Show(this, string.Format(failureFormat, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await RefreshStatusAsync().ConfigureAwait(true);
                SetBusy(false);
            }
        }

        private async Task RefreshStatusAsync()
        {
            try
            {
                ServiceHostStatus status = await ColorVisionServiceHostManager.QueryStatusAsync().ConfigureAwait(true);
                _lastStatus = status;
                UpdateStatusView(status);
                await RefreshCom0ComAsync(status.State == ServiceHostInstallState.Running).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SummaryText.Text = "Status: unknown";
                StateText.Text = "Unknown";
                ActionHintText.Text = "Refresh failed";
                AppendLog(ex.Message);
                HideCom0ComTab();
            }
            finally
            {
                UpdateButtonAvailability();
            }
        }

        private async Task RefreshCom0ComAsync(bool serviceRunning)
        {
            if (!serviceRunning)
            {
                HideCom0ComTab();
                return;
            }

            try
            {
                ServiceHostResponse statusResponse = await ColorVisionServiceHostClient.Default
                    .GetCom0ComStatusAsync(cancellationToken: CancellationToken.None)
                    .ConfigureAwait(true);
                Com0ComStatusInfo? status = statusResponse.Data?.ToObject<Com0ComStatusInfo>();
                if (!statusResponse.Success || status?.Installed != true)
                {
                    HideCom0ComTab();
                    return;
                }

                Com0ComTab.Visibility = Visibility.Visible;
                _com0ComStatus = status;
                UpdateCom0ComView(status);

                ServiceHostResponse listResponse = await ColorVisionServiceHostClient.Default
                    .ListCom0ComPairsAsync(cancellationToken: CancellationToken.None)
                    .ConfigureAwait(true);
                Com0ComStatusInfo? listedStatus = listResponse.Data?.ToObject<Com0ComStatusInfo>();
                if (listResponse.Success && listedStatus?.Installed == true)
                {
                    _com0ComStatus = listedStatus;
                    UpdateCom0ComView(listedStatus);
                }
                else
                {
                    _com0ComStatus = null;
                    Com0ComSummaryText.Text = $"Installed, but pair listing failed: {listResponse.Message}";
                    Com0ComPairsGrid.ItemsSource = null;
                    ClearCom0ComPortChoices();
                    Com0ComPairCountText.Text = "Pair list unavailable";
                }
            }
            catch (Exception ex)
            {
                log.Warn("Failed to query com0com status.", ex);
                HideCom0ComTab();
            }
            finally
            {
                UpdateButtonAvailability();
            }
        }

        private void UpdateCom0ComView(Com0ComStatusInfo status)
        {
            string version = string.IsNullOrWhiteSpace(status.Version) ? "unknown" : status.Version;
            Com0ComSummaryText.Text = $"Version {version} · Driver {status.DriverState}";
            Com0ComPathText.Text = status.SetupExecutablePath;
            Com0ComPairsGrid.ItemsSource = status.Pairs;
            Com0ComPairCountText.Text = status.Pairs.Count == 1 ? "1 pair" : $"{status.Pairs.Count} pairs";
            UpdateCom0ComPortChoices(status);
        }

        private void UpdateCom0ComPortChoices(Com0ComStatusInfo status)
        {
            int? selectedPortA = Com0ComPortAComboBox.SelectedItem is int portA ? portA : null;
            int? selectedPortB = Com0ComPortBComboBox.SelectedItem is int portB ? portB : null;
            Com0ComPortAComboBox.ItemsSource = status.AvailablePortNumbers;
            Com0ComPortBComboBox.ItemsSource = status.AvailablePortNumbers;

            Com0ComPortAComboBox.SelectedItem = selectedPortA.HasValue && status.AvailablePortNumbers.Contains(selectedPortA.Value)
                ? selectedPortA.Value
                : status.SuggestedPair?.PortA;
            Com0ComPortBComboBox.SelectedItem = selectedPortB.HasValue && status.AvailablePortNumbers.Contains(selectedPortB.Value)
                ? selectedPortB.Value
                : status.SuggestedPair?.PortB;
        }

        private void ClearCom0ComPortChoices()
        {
            Com0ComPortAComboBox.ItemsSource = null;
            Com0ComPortBComboBox.ItemsSource = null;
        }

        private void HideCom0ComTab()
        {
            if (ReferenceEquals(ManagerTabs.SelectedItem, Com0ComTab))
                ManagerTabs.SelectedIndex = 0;
            Com0ComTab.Visibility = Visibility.Collapsed;
            _com0ComStatus = null;
            Com0ComPairsGrid.ItemsSource = null;
            ClearCom0ComPortChoices();
            Com0ComSummaryText.Text = "Not available";
            Com0ComPathText.Text = string.Empty;
            Com0ComPairCountText.Text = string.Empty;
        }

        private void UpdateStatusView(ServiceHostStatus status)
        {
            SummaryText.Text = status.DisplayText;
            StateText.Text = status.State.ToString();
            PackageVersionText.Text = FormatVersion(status.PackageVersion);
            InstalledVersionText.Text = FormatVersion(status.InstalledVersion);
            RunningVersionText.Text = FormatVersion(status.RunningVersion);
            PackagePathText.Text = status.PackageExecutablePath;
            InstalledPathText.Text = status.InstalledExecutablePath;
            RunningProcessText.Text = string.IsNullOrWhiteSpace(status.RunningProcessPath) ? "-" : status.RunningProcessPath;
            LogPathText.Text = ServiceHostLogPath;
            ActionHintText.Text = GetActionHint(status);
        }

        private static string GetActionHint(ServiceHostStatus status)
        {
            if (status.NeedsInstall)
                return status.IsPackageAvailable ? "Install service host" : "Package missing";
            if (status.NeedsUpdate)
                return status.CanSelfUpdate ? "Self update available" : "Install / update available";
            if (status.State == ServiceHostInstallState.Stopped)
                return "Start service host";
            if (status.State == ServiceHostInstallState.Running)
                return "Ready";

            return "Check service host";
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            UpdateButtonAvailability();
        }

        private void UpdateButtonAvailability()
        {
            bool enabled = !_isBusy;
            bool isRunning = _lastStatus?.State == ServiceHostInstallState.Running;
            bool isStopped = _lastStatus?.State == ServiceHostInstallState.Stopped;
            bool isInstalled = isRunning || isStopped || _lastStatus?.State == ServiceHostInstallState.Unknown;

            RefreshButton.IsEnabled = enabled;
            InstallButton.IsEnabled = enabled;
            SelfUpdateButton.IsEnabled = enabled && _lastStatus?.CanSelfUpdate == true;
            StartButton.IsEnabled = enabled && isStopped;
            StopButton.IsEnabled = enabled && isRunning;
            UninstallButton.IsEnabled = enabled && isInstalled;
            PingButton.IsEnabled = enabled && isRunning;
            StatusButton.IsEnabled = enabled && isRunning;
            FileAssociationButton.IsEnabled = enabled && isRunning;
            RegisterThumbnailButton.IsEnabled = enabled && isRunning;
            UnregisterThumbnailButton.IsEnabled = enabled && isRunning;
            WriteMarkerButton.IsEnabled = enabled && isRunning;
            OpenPackageButton.IsEnabled = enabled;
            OpenInstalledButton.IsEnabled = enabled;
            OpenLogButton.IsEnabled = enabled;
            CopyStatusButton.IsEnabled = enabled;
            bool canManageCom0Com = enabled && isRunning && _com0ComStatus?.Installed == true;
            bool hasValidPortSelection = Com0ComPortAComboBox.SelectedItem is int portA
                && Com0ComPortBComboBox.SelectedItem is int portB
                && portA != portB;
            Com0ComRefreshButton.IsEnabled = enabled && isRunning;
            Com0ComCreateButton.IsEnabled = canManageCom0Com && hasValidPortSelection;
            Com0ComDeleteButton.IsEnabled = canManageCom0Com && Com0ComPairsGrid.SelectedItem is Com0ComPairInfo;
        }

        private static string FormatVersion(Version? version)
        {
            return version?.ToString() ?? "unknown";
        }

        private static string BuildStatusSnapshot(ServiceHostStatus status)
        {
            StringBuilder builder = new();
            builder.AppendLine($"Service: {ServiceHostProtocol.ServiceName}");
            builder.AppendLine($"State: {status.State}");
            builder.AppendLine($"PackageVersion: {FormatVersion(status.PackageVersion)}");
            builder.AppendLine($"InstalledVersion: {FormatVersion(status.InstalledVersion)}");
            builder.AppendLine($"RunningVersion: {FormatVersion(status.RunningVersion)}");
            builder.AppendLine($"NeedsInstall: {status.NeedsInstall}");
            builder.AppendLine($"NeedsUpdate: {status.NeedsUpdate}");
            builder.AppendLine($"PackagePath: {status.PackageExecutablePath}");
            builder.AppendLine($"InstalledPath: {status.InstalledExecutablePath}");
            builder.AppendLine($"RunningProcess: {status.RunningProcessPath}");
            builder.AppendLine($"LogPath: {ServiceHostLogPath}");
            builder.AppendLine($"RawOutput: {status.RawOutput}");
            return builder.ToString();
        }

        private void OpenPath(string path, string missingMessage)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true,
                    });
                    return;
                }

                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                    });
                    return;
                }

                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = directory,
                        UseShellExecute = true,
                    });
                    return;
                }

                MessageBox.Show(this, $"{missingMessage}{Environment.NewLine}{path}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                AppendLog(ex.Message);
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as an instance helper for operation logging.")]
        private void AppendLog(string message)
        {
            log.Info(message);
        }

    }
}
