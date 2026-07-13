using ColorVision.Common.Utilities;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Desktop.Operations;
using ColorVision.UI.ServiceHost;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Desktop.LanRemote
{
    public partial class LanRemoteControlSettingsControl : UserControl
    {
        private bool _isRefreshing;
        private string _pairingPayload = string.Empty;

        public LanRemoteControlSettingsControl()
        {
            InitializeComponent();
            Loaded += LanRemoteControlSettingsControl_Loaded;
            Unloaded += LanRemoteControlSettingsControl_Unloaded;
        }

        private static LanRemoteControlConfig Config => LanRemoteControlConfig.Instance;

        private static LanRemoteControlService Service => LanRemoteControlService.Instance;

        private const string AutoAddressValue = "";

        private void LanRemoteControlSettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Config.EnsureInitialized())
                ConfigHandler.GetInstance().Save<LanRemoteControlConfig>();

            Service.StateChanged += Service_StateChanged;
            Service.ApplyConfig();
            RefreshUi();
        }

        private void LanRemoteControlSettingsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Service.StateChanged -= Service_StateChanged;
        }

        private void Service_StateChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(RefreshUi);
        }

        private void EnableCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;

            Config.IsEnabled = EnableCheckBox.IsChecked == true;
            SaveAndApply();
        }

        private void PortTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyPortFromTextBox();
        }

        private void PortTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            ApplyPortFromTextBox();
            e.Handled = true;
        }

        private void ApplyPortButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyPortFromTextBox();
        }

        private void IpAddressComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing) return;

            if (IpAddressComboBox.SelectedItem is not IpAddressOption option)
                return;

            Config.PreferredHost = option.Address;
            SaveAndApply();
        }

        private void OpenAppDownloadPageButton_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.Open(AppDownloadUrlTextBox.Text);
        }

        private void CopyAppDownloadUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(AppDownloadUrlTextBox.Text);
            StatusTextBlock.Text = "App 下载地址已复制。";
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ConnectionUrlTextBox.Text);
            StatusTextBlock.Text = "连接地址已复制。";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Service.ApplyConfig();
            RefreshUi();
        }

        private void ResetTokenButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPairingPayload();
            RefreshUi();
        }

        private void ApproveDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PendingDevicesListBox.SelectedItem is not OperationsPairingClaim claim)
            {
                StatusTextBlock.Text = "请先选择要批准的设备。";
                return;
            }

            if (Service.OperationsHost.Pairing.Approve(claim.PairingId))
                StatusTextBlock.Text = $"已批准 {claim.DeviceName} 的只读运维权限。";
            RefreshUi();
        }

        private void RejectDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PendingDevicesListBox.SelectedItem is not OperationsPairingClaim claim)
            {
                StatusTextBlock.Text = "请先选择要拒绝的设备。";
                return;
            }

            if (Service.OperationsHost.Pairing.Reject(claim.PairingId))
                StatusTextBlock.Text = $"已拒绝 {claim.DeviceName}。";
            RefreshUi();
        }

        private void RevokeDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PairedDevicesListBox.SelectedItem is not OperationsPairedDevice device)
            {
                StatusTextBlock.Text = "请先选择要撤销的设备。";
                return;
            }

            if (Service.OperationsHost.Registry.Revoke(device.DeviceId))
                StatusTextBlock.Text = $"已撤销 {device.DisplayName}。";
            RefreshUi();
        }

        private async void LocalCoSignJobButton_Click(object sender, RoutedEventArgs e)
        {
            if (LocalCoSignJobsListBox.SelectedItem is not OperationsJob job)
            {
                StatusTextBlock.Text = "请先选择要本机确认的作业。";
                return;
            }

            string evidenceId = string.Empty;
            if (job.CapabilityId == "ops.diagnostics.bundle.create")
            {
                OperationsDiagnosticBundleResult bundle = Service.OperationsHost.CreateDiagnosticBundle();
                evidenceId = bundle.BundleId;
            }
            OperationsJob? approvedJob = Service.OperationsHost.WorkStore.LocalCoSign(job.JobId, true, evidenceId);
            if (approvedJob?.CapabilityId == "ops.service.restart")
            {
                string serviceId = approvedJob.Input.TryGetProperty("serviceId", out System.Text.Json.JsonElement serviceElement)
                    ? serviceElement.GetString() ?? string.Empty : string.Empty;
                if (serviceId != "mosquitto")
                {
                    Service.OperationsHost.WorkStore.CompleteJob(job.JobId, false, "service_not_in_operations_allowlist");
                }
                else
                {
                    try
                    {
                        ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                            .RestartServiceAsync("mosquitto", timeoutSeconds: 60, timeout: TimeSpan.FromSeconds(90));
                        Service.OperationsHost.WorkStore.CompleteJob(job.JobId, response.Success,
                            $"servicehost:{response.RequestId}");
                    }
                    catch (Exception ex)
                    {
                        Service.OperationsHost.WorkStore.CompleteJob(job.JobId, false,
                            $"servicehost_error:{ex.GetType().Name}");
                    }
                }
            }
            else if (approvedJob?.CapabilityId == "ops.diagnostics.bundle.create")
            {
                Service.OperationsHost.WorkStore.CompleteJob(job.JobId, true, evidenceId);
            }
            RefreshUi();
        }

        private void LocalRejectJobButton_Click(object sender, RoutedEventArgs e)
        {
            if (LocalCoSignJobsListBox.SelectedItem is OperationsJob job)
                Service.OperationsHost.WorkStore.LocalCoSign(job.JobId, false);
            RefreshUi();
        }

        private void ConsentSupportButton_Click(object sender, RoutedEventArgs e)
        {
            if (SupportRequestsListBox.SelectedItem is OperationsSupportSession session)
                Service.OperationsHost.WorkStore.LocalConsentSupport(session.SessionId, true);
            RefreshUi();
        }

        private void RejectSupportButton_Click(object sender, RoutedEventArgs e)
        {
            if (SupportRequestsListBox.SelectedItem is OperationsSupportSession session)
                Service.OperationsHost.WorkStore.LocalConsentSupport(session.SessionId, false);
            RefreshUi();
        }

        private void ApplyPortFromTextBox()
        {
            if (!int.TryParse(PortTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
                port = LanRemoteControlConfig.DefaultPort;

            Config.Port = LanRemoteControlConfig.NormalizePort(port);
            SaveAndApply();
        }

        private void SaveAndApply()
        {
            ConfigHandler.GetInstance().Save<LanRemoteControlConfig>();
            Service.ApplyConfig();
            RefreshUi();
        }

        private void RefreshUi()
        {
            _isRefreshing = true;
            try
            {
                EnableCheckBox.IsChecked = Config.IsEnabled;
                PortTextBox.Text = Config.Port.ToString(CultureInfo.InvariantCulture);
                SecurePortTextBlock.Text = Config.SecurePort.ToString(CultureInfo.InvariantCulture);

                var addresses = LanRemoteControlService.GetLocalIpAddresses();
                RefreshIpAddressOptions(addresses);

                string appDownloadUrl = GetAppDownloadUrl();
                AppDownloadUrlTextBox.Text = appDownloadUrl;
                AppDownloadQrImage.Source = LanRemoteQrCode.Create(appDownloadUrl);

                string connectionUrl = Service.GetSecureBaseUrl();
                ConnectionUrlTextBox.Text = connectionUrl;
                if (Service.OperationsHost.IsRunning && string.IsNullOrWhiteSpace(_pairingPayload))
                    RefreshPairingPayload();
                QrImage.Source = string.IsNullOrWhiteSpace(_pairingPayload) ? null : LanRemoteQrCode.Create(_pairingPayload);
                QrCard.Opacity = Service.OperationsHost.IsRunning ? 1 : 0.38;

                StatusTextBlock.Text = Service.LastStatusMessage;
                ServiceStateTextBlock.Text = Service.OperationsHost.IsRunning ? "安全通道运行中" : Config.IsEnabled ? "安全通道启动失败" : "未启用";
                PendingDevicesListBox.ItemsSource = Service.OperationsHost.GetPendingClaims();
                PairedDevicesListBox.ItemsSource = Service.OperationsHost.Registry.GetAll().Where(item => item.IsActive).ToList();
                LocalCoSignJobsListBox.ItemsSource = Service.OperationsHost.WorkStore.GetJobs()
                    .Where(item => item.Status == "awaiting_local_cosign").ToList();
                SupportRequestsListBox.ItemsSource = Service.OperationsHost.WorkStore.GetSupportSessions()
                    .Where(item => item.Status == "awaiting_local_consent" && item.ExpiresAt > DateTimeOffset.UtcNow).ToList();

                IpListTextBlock.Text = addresses.Count == 0
                    ? "未检测到可用的局域网 IPv4 地址，请确认电脑和手机在同一个 Wi-Fi 或网段。"
                    : $"可用地址：{string.Join(", ", addresses)}";
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RefreshPairingPayload()
        {
            _pairingPayload = Service.OperationsHost.IsRunning ? Service.CreateSecurePairingPayload() : string.Empty;
        }

        private void RefreshIpAddressOptions(IReadOnlyList<string> addresses)
        {
            string selectedAddress = string.IsNullOrWhiteSpace(Config.PreferredHost)
                ? AutoAddressValue
                : Config.PreferredHost;

            IpAddressComboBox.Items.Clear();
            string autoText = addresses.Count > 0
                ? $"自动选择（{addresses[0]}）"
                : "自动选择";
            IpAddressComboBox.Items.Add(new IpAddressOption(autoText, AutoAddressValue));

            foreach (string address in addresses)
            {
                IpAddressComboBox.Items.Add(new IpAddressOption(address, address));
            }

            foreach (object item in IpAddressComboBox.Items)
            {
                if (item is IpAddressOption option
                    && string.Equals(option.Address, selectedAddress, StringComparison.Ordinal))
                {
                    IpAddressComboBox.SelectedItem = option;
                    return;
                }
            }

            IpAddressComboBox.SelectedIndex = 0;
        }

        private static string GetAppDownloadUrl()
        {
            return MarketplaceConfig.BuildApiUrl("releases");
        }

        private sealed class IpAddressOption
        {
            public IpAddressOption(string displayName, string address)
            {
                DisplayName = displayName;
                Address = address;
            }

            public string DisplayName { get; }

            public string Address { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
