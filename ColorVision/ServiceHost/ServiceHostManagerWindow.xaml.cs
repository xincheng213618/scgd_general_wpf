#pragma warning disable CA1863
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ColorVision.Themes;
using ColorVision.UI.Desktop.Feedback;
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
        private static readonly string InstallLogPath = Path.Combine(
            ServiceHostProtocol.InstallDirectory,
            "install.log");

        private ServiceHostStatus? _lastStatus;
        private Com0ComStatusInfo? _com0ComStatus;
        private bool _isBusy;
        private bool _isRefreshingLogs;
        private readonly ModuleLogViewerBinder _logBinder;
        private readonly DispatcherTimer _logRefreshTimer;
        private long _serviceLogLength = -1;
        private DateTime _serviceLogWriteTimeUtc = DateTime.MinValue;
        private long _installLogLength = -1;
        private DateTime _installLogWriteTimeUtc = DateTime.MinValue;
        private string _lastInstallationFailure = string.Empty;

        public ServiceHostManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            _logBinder = new ModuleLogViewerBinder(LogViewer, "ColorVision.ServiceHost");
            _logRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _logRefreshTimer.Tick += LogRefreshTimer_Tick;
            InitializeStaticText();
            Loaded += ServiceHostManagerWindow_Loaded;
            Closed += ServiceHostManagerWindow_Closed;
        }

        private async void ServiceHostManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AppendLog("Service Host 管理页面已打开。");
            await RefreshStatusAsync().ConfigureAwait(true);
            _logRefreshTimer.Start();
        }

        private void ServiceHostManagerWindow_Closed(object? sender, EventArgs e)
        {
            _logRefreshTimer.Stop();
            _logBinder.Dispose();
        }

        private async void LogRefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (AutoRefreshLogsCheckBox.IsChecked == true)
                await RefreshFileLogsAsync(force: false).ConfigureAwait(true);
        }

        private void InitializeStaticText()
        {
            ServiceNameText.Text = ServiceHostProtocol.ServiceName;
            SummaryText.Text = "正在检查服务状态…";
            ActionHintText.Text = "建议操作：刷新";
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync().ConfigureAwait(true);
        }

        private async void RefreshLogsButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshFileLogsAsync(force: true).ConfigureAwait(true);
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
            string path = ReferenceEquals(LogTabs.SelectedItem, InstallLogTab) ? InstallLogPath : ServiceHostLogPath;
            OpenPath(path, "日志文件不存在。");
        }

        private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenPath(Path.GetDirectoryName(ServiceHostLogPath) ?? ServiceHostProtocol.InstallDirectory, "日志目录不存在。");
        }

        private void SendFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            string[] attachments = new[] { ServiceHostLogPath, InstallLogPath }
                .Where(File.Exists)
                .ToArray();
            FeedbackWindow window = new(
                "ColorVision 服务主机问题\n\n现象：\n\n复现步骤：\n",
                attachments)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.ShowDialog();
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
            string failureMessage = string.Empty;
            try
            {
                ServiceHostOperationResult result = await operation(CancellationToken.None).ConfigureAwait(true);
                AppendLog(result.Summary);
                if (!result.Success)
                {
                    failureMessage = string.IsNullOrWhiteSpace(result.Error)
                        ? $"操作未成功，退出码 {result.ExitCode}。"
                        : result.Error.Trim();
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
                failureMessage = ex.Message;
            }
            finally
            {
                await RefreshStatusAsync().ConfigureAwait(true);
                SetBusy(false);
            }

            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                if (name.Contains("Install", StringComparison.OrdinalIgnoreCase))
                    LogTabs.SelectedItem = InstallLogTab;
                MessageBox.Show(
                    this,
                    $"{name} 失败。{Environment.NewLine}{failureMessage}{Environment.NewLine}{Environment.NewLine}已保留安装记录和服务日志供诊断。",
                    "ColorVision 服务主机",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                SummaryText.Text = "无法读取服务状态";
                SummaryText.Visibility = Visibility.Visible;
                VersionBadgeText.Text = "当前版本 未知";
                StateText.Text = "未知";
                ActionHintText.Text = "建议操作：检查日志后重试";
                AppendLog(ex.Message);
                HideCom0ComTab();
            }
            finally
            {
                UpdateButtonAvailability();
                await RefreshFileLogsAsync(force: false).ConfigureAwait(true);
            }
        }

        private async Task RefreshFileLogsAsync(bool force)
        {
            if (_isRefreshingLogs)
                return;

            _isRefreshingLogs = true;
            try
            {
                (ServiceHostLogSnapshot serviceLog, ServiceHostLogSnapshot installLog) = await Task.Run(() =>
                {
                    ServiceHostLogSnapshot serviceSnapshot = ServiceHostLogReader.ReadTail(ServiceHostLogPath, 180_000);
                    ServiceHostLogSnapshot installSnapshot = ServiceHostLogReader.ReadTail(InstallLogPath, 120_000);
                    return (serviceSnapshot, installSnapshot);
                }).ConfigureAwait(true);

                ApplyLogSnapshot(
                    ServiceLogViewer,
                    ServiceLogMetaText,
                    serviceLog,
                    "服务运行日志",
                    ref _serviceLogLength,
                    ref _serviceLogWriteTimeUtc,
                    force);
                ApplyLogSnapshot(
                    InstallLogViewer,
                    InstallLogMetaText,
                    installLog,
                    "安装记录",
                    ref _installLogLength,
                    ref _installLogWriteTimeUtc,
                    force);

                _lastInstallationFailure = ServiceHostLogReader.GetLatestInstallationFailure(installLog.Text);
                if (_lastStatus != null)
                    UpdateHealthView(_lastStatus);
            }
            finally
            {
                _isRefreshingLogs = false;
            }
        }

        private static void ApplyLogSnapshot(
            ColorVision.UI.LogImp.Controls.LogViewerControl viewer,
            System.Windows.Controls.TextBlock metadataText,
            ServiceHostLogSnapshot snapshot,
            string label,
            ref long previousLength,
            ref DateTime previousWriteTimeUtc,
            bool force)
        {
            if (!snapshot.Exists)
            {
                metadataText.Text = $"{label}尚未创建";
                if (force || previousLength != 0)
                    viewer.SetText("当前还没有日志。服务首次运行或执行安装后，记录会自动显示在这里。", latestAtTop: false);
                previousLength = 0;
                previousWriteTimeUtc = DateTime.MinValue;
                return;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.Error))
            {
                metadataText.Text = $"{label}读取失败";
                viewer.SetText($"无法读取日志：{snapshot.Error}", latestAtTop: false);
                previousLength = -1;
                previousWriteTimeUtc = DateTime.MinValue;
                return;
            }

            metadataText.Text = snapshot.Length == 0
                ? $"{label}为空"
                : $"{label} · {FormatFileSize(snapshot.Length)} · {snapshot.LastWriteTimeUtc.ToLocalTime():HH:mm:ss} 更新 · 显示最新内容";
            if (force || snapshot.Length != previousLength || snapshot.LastWriteTimeUtc != previousWriteTimeUtc)
                viewer.SetText(string.IsNullOrWhiteSpace(snapshot.Text) ? "日志文件为空。" : snapshot.Text, latestAtTop: false);

            previousLength = snapshot.Length;
            previousWriteTimeUtc = snapshot.LastWriteTimeUtc;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024L * 1024L)
                return $"{bytes / 1024d / 1024d:0.##} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024d:0.##} KB";
            return $"{bytes} B";
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
            UpdateVersionPresentation(status);
            StateText.Text = FormatState(status.State);
            ActionHintText.Text = GetActionHint(status);
            ConnectionText.Text = status.State == ServiceHostInstallState.Running && status.RunningVersion != null
                ? "连接正常"
                : status.State == ServiceHostInstallState.Running ? "服务无响应" : "未连接";
            UpdateIntegrityView(status.RuntimeIntegrity);
            UpdateHealthView(status);
            UpdateStateBadge(status.State);
        }

        private void UpdateVersionPresentation(ServiceHostStatus status)
        {
            Version? displayVersion = status.RunningVersion ?? status.InstalledVersion ?? status.PackageVersion;
            string versionText = FormatVersion(displayVersion);
            VersionBadgeText.Text = $"当前版本 {versionText}";
            Title = displayVersion == null ? "ColorVision 服务主机" : $"ColorVision 服务主机 {versionText}";

            string detail = BuildSummaryText(status);
            SummaryText.Text = detail;
            SummaryText.Visibility = string.IsNullOrWhiteSpace(detail) ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string GetActionHint(ServiceHostStatus status)
        {
            if (status.HasIncompletePackage)
                return "建议操作：使用完整安装包修复 ColorVision";
            if (status.HasIncompleteInstalledRuntime)
                return "建议操作：重新安装并修复服务";
            if (status.NeedsInstall)
                return status.IsPackageAvailable ? "建议操作：安装服务" : "建议操作：使用完整安装包修复";
            if (status.NeedsUpdate)
                return status.CanSelfUpdate ? "建议操作：后台更新服务" : "建议操作：安装或更新服务";
            if (status.State == ServiceHostInstallState.Stopped)
                return "建议操作：启动服务";
            if (status.State == ServiceHostInstallState.Running)
                return "当前无需处理";

            return "建议操作：刷新并检查日志";
        }

        private void UpdateHealthView(ServiceHostStatus status)
        {
            string title;
            string description;
            string icon;
            string foregroundKey;
            string backgroundKey;
            string installButtonText;
            string installButtonStyle;

            if (status.HasIncompletePackage)
            {
                title = "服务程序包不完整";
                description = $"当前 ColorVision 程序包缺少 {status.RuntimeIntegrity.MissingPackageFiles.Count} 个服务文件：{BuildFileIssuePreview(status.RuntimeIntegrity.MissingPackageFiles)}。请使用完整安装包修复。";
                icon = "\uE783";
                foregroundKey = "ServiceHost.Error";
                backgroundKey = "ServiceHost.ErrorBackground";
                installButtonText = "需要完整安装包";
                installButtonStyle = "PrimaryActionButtonStyle";
            }
            else if (status.HasIncompleteInstalledRuntime)
            {
                title = "服务安装不完整";
                description = $"检测到 {status.RuntimeIntegrity.InstalledIssueCount} 个缺失或不一致的文件：{BuildInstalledIssuePreview(status.RuntimeIntegrity)}。重新安装会从当前完整程序包恢复这些文件。";
                icon = "\uE783";
                foregroundKey = "ServiceHost.Error";
                backgroundKey = "ServiceHost.ErrorBackground";
                installButtonText = "重新安装并修复";
                installButtonStyle = "PrimaryActionButtonStyle";
            }
            else if (!string.IsNullOrWhiteSpace(_lastInstallationFailure))
            {
                title = "上次安装未完成";
                description = $"安装记录显示失败：{TrimLogPrefix(_lastInstallationFailure)}。当前服务状态为“{FormatState(status.State)}”，建议重新安装并验证。";
                icon = "\uE7BA";
                foregroundKey = "ServiceHost.Warning";
                backgroundKey = "ServiceHost.WarningBackground";
                installButtonText = "重新安装并验证";
                installButtonStyle = "PrimaryActionButtonStyle";
            }
            else if (status.NeedsInstall)
            {
                title = "服务尚未安装";
                description = "系统维护服务不可用，更新和需要管理员权限的操作可能无法完成。";
                icon = "\uE783";
                foregroundKey = "ServiceHost.Error";
                backgroundKey = "ServiceHost.ErrorBackground";
                installButtonText = "安装服务";
                installButtonStyle = "PrimaryActionButtonStyle";
            }
            else if (status.NeedsUpdate || status.NeedsRepair || status.State != ServiceHostInstallState.Running)
            {
                title = status.State == ServiceHostInstallState.Stopped ? "服务已停止" : "服务需要处理";
                description = status.NeedsUpdate
                    ? "程序包与当前服务版本或内容不一致，建议更新后重新检查。"
                    : "服务没有处于可用状态，请按建议操作恢复。";
                icon = "\uE7BA";
                foregroundKey = "ServiceHost.Warning";
                backgroundKey = "ServiceHost.WarningBackground";
                installButtonText = status.State == ServiceHostInstallState.Stopped ? "安装或修复服务" : "更新或修复服务";
                installButtonStyle = "PrimaryActionButtonStyle";
            }
            else
            {
                title = "服务运行正常";
                description = status.RuntimeIntegrity.CanEvaluate
                    ? $"服务连接正常，版本一致，已核对 {status.RuntimeIntegrity.ExpectedFiles.Count} 个运行时文件。"
                    : "服务连接正常，当前版本可以使用。";
                icon = "\uE930";
                foregroundKey = "ServiceHost.Success";
                backgroundKey = "ServiceHost.SuccessBackground";
                installButtonText = "重新安装服务";
                installButtonStyle = "CompactActionButtonStyle";
            }

            HealthTitleText.Text = title;
            HealthDescriptionText.Text = description;
            HealthIconText.Text = icon;
            HealthIconText.Foreground = (Brush)FindResource(foregroundKey);
            HealthIconBorder.Background = (Brush)FindResource(backgroundKey);
            InstallButton.Content = installButtonText;
            InstallButton.Style = (Style)FindResource(installButtonStyle);
            ActionHintText.Text = GetActionHint(status);
        }

        private void UpdateIntegrityView(ServiceHostRuntimeIntegrity integrity)
        {
            if (!integrity.CanEvaluate)
            {
                IntegrityText.Text = "无法核对";
                IntegrityDetailText.Text = "服务程序包目录不可用";
                return;
            }

            if (!integrity.IsPackageComplete)
            {
                IntegrityText.Text = $"程序包缺少 {integrity.MissingPackageFiles.Count} 个文件";
                IntegrityDetailText.Text = BuildFileIssuePreview(integrity.MissingPackageFiles);
                return;
            }

            if (!integrity.IsInstalledComplete)
            {
                IntegrityText.Text = $"发现 {integrity.InstalledIssueCount} 个文件问题";
                IntegrityDetailText.Text = BuildInstalledIssuePreview(integrity);
                return;
            }

            IntegrityText.Text = $"{integrity.ExpectedFiles.Count} 个文件完整";
            IntegrityDetailText.Text = "程序包与安装目录一致";
        }

        private void UpdateStateBadge(ServiceHostInstallState state)
        {
            string foregroundKey = state == ServiceHostInstallState.Running
                ? "ServiceHost.Success"
                : state == ServiceHostInstallState.Stopped ? "ServiceHost.Warning" : "ServiceHost.Error";
            string backgroundKey = state == ServiceHostInstallState.Running
                ? "ServiceHost.SuccessBackground"
                : state == ServiceHostInstallState.Stopped ? "ServiceHost.WarningBackground" : "ServiceHost.ErrorBackground";
            StateText.Foreground = (Brush)FindResource(foregroundKey);
            StateBadgeBorder.Background = (Brush)FindResource(backgroundKey);
        }

        private static string BuildSummaryText(ServiceHostStatus status)
        {
            if (status.RunningVersion != null
                && status.InstalledVersion != null
                && status.PackageVersion != null
                && status.RunningVersion == status.InstalledVersion
                && status.InstalledVersion == status.PackageVersion)
            {
                return string.Empty;
            }

            return $"运行 {FormatVersion(status.RunningVersion)} · 安装 {FormatVersion(status.InstalledVersion)} · 程序包 {FormatVersion(status.PackageVersion)}";
        }

        private static string FormatState(ServiceHostInstallState state)
        {
            return state switch
            {
                ServiceHostInstallState.Running => "运行中",
                ServiceHostInstallState.Stopped => "已停止",
                ServiceHostInstallState.NotInstalled => "未安装",
                _ => "未知",
            };
        }

        private static string BuildInstalledIssuePreview(ServiceHostRuntimeIntegrity integrity)
        {
            IEnumerable<string> issues = integrity.MissingInstalledFiles
                .Select(path => $"缺少 {path}")
                .Concat(integrity.MismatchedInstalledFiles.Select(path => $"不一致 {path}"));
            return BuildFileIssuePreview(issues);
        }

        private static string BuildFileIssuePreview(IEnumerable<string> paths)
        {
            string[] items = paths.Take(3).ToArray();
            int totalCount = paths.Count();
            string preview = items.Length == 0 ? "未提供文件详情" : string.Join("、", items);
            return totalCount > items.Length ? $"{preview}，另有 {totalCount - items.Length} 个" : preview;
        }

        private static string TrimLogPrefix(string line)
        {
            int closingBracket = line.IndexOf(']');
            return closingBracket >= 0 && closingBracket + 1 < line.Length
                ? line[(closingBracket + 1)..].Trim()
                : line.Trim();
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
            InstallButton.IsEnabled = enabled
                && _lastStatus?.IsPackageAvailable == true
                && _lastStatus.RuntimeIntegrity.IsPackageComplete
                && !_lastStatus.WouldInstallDowngrade;
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
            OpenLogFolderButton.IsEnabled = enabled;
            OpenLogButton.IsEnabled = enabled;
            CopyStatusButton.IsEnabled = enabled;
            SendFeedbackButton.IsEnabled = enabled;
            RefreshLogsButton.IsEnabled = enabled;
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
            builder.AppendLine($"PackageComplete: {status.RuntimeIntegrity.IsPackageComplete}");
            builder.AppendLine($"InstalledComplete: {status.RuntimeIntegrity.IsInstalledComplete}");
            builder.AppendLine($"MissingPackageFiles: {string.Join(", ", status.RuntimeIntegrity.MissingPackageFiles)}");
            builder.AppendLine($"MissingInstalledFiles: {string.Join(", ", status.RuntimeIntegrity.MissingInstalledFiles)}");
            builder.AppendLine($"MismatchedInstalledFiles: {string.Join(", ", status.RuntimeIntegrity.MismatchedInstalledFiles)}");
            builder.AppendLine($"LogPath: {ServiceHostLogPath}");
            builder.AppendLine($"InstallLogPath: {InstallLogPath}");
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
