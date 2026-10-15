using ColorVision.Common.Utilities;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Desktop.Operations;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Desktop.LanRemote
{
    public partial class LanRemoteControlSettingsControl : UserControl
    {
        private bool _isRefreshing;
        private OperationsPairingChallenge? _pairingChallenge;
        private bool _pairingClaimed;
        private DateTime? _pairingHostStartedAt;
        private readonly DispatcherTimer _pairingTimer;

        public LanRemoteControlSettingsControl()
        {
            InitializeComponent();
            ServiceStateTextBlock.Text = LanRemoteText.Get("Disabled");
            PairingStateTextBlock.Text = LanRemoteText.Get("EnableToPair");
            ResetTokenButton.IsEnabled = false;
            CopyUrlButton.IsEnabled = false;
            _pairingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pairingTimer.Tick += (_, _) => RefreshPairingState();
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
            _pairingTimer.Start();
        }

        private void LanRemoteControlSettingsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Service.StateChanged -= Service_StateChanged;
            _pairingTimer.Stop();
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
            StatusTextBlock.Text = LanRemoteText.Get("DownloadCopied");
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ConnectionUrlTextBox.Text);
            StatusTextBlock.Text = LanRemoteText.Get("AddressCopied");
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Service.ApplyConfig();
            RefreshUi();
            StatusTextBlock.Text = LanRemoteText.Get("StatusUpdated");
        }

        private void ResetTokenButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPairingPayload();
            RefreshUi();
            StatusTextBlock.Text = LanRemoteText.Get("PairingUpdated");
        }

        private void ApproveDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PendingDevicesListBox.SelectedItem is not OperationsPairingClaim claim)
            {
                StatusTextBlock.Text = LanRemoteText.Get("SelectApproveDevice");
                return;
            }

            bool approved = Service.OperationsHost.Pairing.Approve(claim.PairingId);
            RefreshUi();
            StatusTextBlock.Text = approved ? LanRemoteText.Format("DeviceApproved", claim.DeviceName) : LanRemoteText.Get("RequestUnavailable");
        }

        private void RejectDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PendingDevicesListBox.SelectedItem is not OperationsPairingClaim claim)
            {
                StatusTextBlock.Text = LanRemoteText.Get("SelectRejectDevice");
                return;
            }

            bool rejected = Service.OperationsHost.Pairing.Reject(claim.PairingId);
            RefreshUi();
            StatusTextBlock.Text = rejected ? LanRemoteText.Format("DeviceRejected", claim.DeviceName) : LanRemoteText.Get("RequestUnavailable");
        }

        private void RevokeDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PairedDevicesListBox.SelectedItem is not OperationsPairedDevice device)
            {
                StatusTextBlock.Text = LanRemoteText.Get("SelectRevokeDevice");
                return;
            }

            bool revoked = Service.OperationsHost.Registry.Revoke(device.DeviceId);
            RefreshUi();
            StatusTextBlock.Text = revoked ? LanRemoteText.Format("DeviceRevoked", device.DisplayName) : LanRemoteText.Get("RequestUnavailable");
        }

        private void LocalCoSignJobButton_Click(object sender, RoutedEventArgs e)
        {
            if (LocalCoSignJobsListBox.SelectedItem is not OperationsJob job)
            {
                StatusTextBlock.Text = LanRemoteText.Get("SelectJob");
                return;
            }

            if (job.CapabilityId == "ops.window.snapshot.capture"
                && MessageBox.Show(Window.GetWindow(this),
                    LanRemoteText.Get("SnapshotConfirmation"),
                    LanRemoteText.Get("SnapshotConfirmationTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                    != MessageBoxResult.OK)
                return;

            string evidenceId = string.Empty;
            try
            {
                if (job.CapabilityId == "ops.diagnostics.bundle.create")
                {
                    OperationsDiagnosticBundleResult bundle = Service.OperationsHost.CreateDiagnosticBundle();
                    evidenceId = bundle.BundleId;
                }
                else if (job.CapabilityId == "ops.window.snapshot.capture")
                {
                    OperationsWindowSnapshotResult snapshot = Service.OperationsHost.CreateWindowSnapshot();
                    evidenceId = OperationsWindowSnapshotService.EvidencePrefix + snapshot.SnapshotId;
                }
            }
            catch (Exception ex)
            {
                RefreshUi();
                StatusTextBlock.Text = LanRemoteText.Format("EvidenceFailed", ex.Message);
                return;
            }
            OperationsJob? approvedJob = Service.OperationsHost.WorkStore.LocalCoSign(job.JobId, true, evidenceId);
            if (approvedJob?.CapabilityId == "ops.diagnostics.bundle.create")
            {
                Service.OperationsHost.WorkStore.CompleteJob(job.JobId, true, evidenceId);
            }
            else if (approvedJob?.CapabilityId == "ops.window.snapshot.capture")
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
                if (!PortTextBox.IsKeyboardFocusWithin)
                    PortTextBox.Text = Config.Port.ToString(CultureInfo.InvariantCulture);
                SecurePortTextBlock.Text = Config.SecurePort.ToString(CultureInfo.InvariantCulture);

                var addresses = LanRemoteControlService.GetLocalIpAddresses();
                RefreshIpAddressOptions(addresses);

                string appDownloadUrl = GetAppDownloadUrl();
                if (AppDownloadUrlTextBox.Text != appDownloadUrl)
                {
                    AppDownloadUrlTextBox.Text = appDownloadUrl;
                    AppDownloadQrImage.Source = LanRemoteQrCode.Create(appDownloadUrl);
                }

                string connectionUrl = Service.GetSecureBaseUrl();
                ConnectionUrlTextBox.Text = connectionUrl;
                bool running = Service.OperationsHost.IsRunning;
                if (!running)
                {
                    _pairingChallenge = null;
                    QrImage.Source = null;
                }
                else if (_pairingChallenge == null || _pairingChallenge.Endpoint != connectionUrl
                    || _pairingHostStartedAt != Service.StartedAt)
                    RefreshPairingPayload();

                NetworkStatusTextBlock.Text = Service.OperationsHost.LastStatusMessage + Environment.NewLine + Service.LastStatusMessage;
                ServiceStateTextBlock.Text = LanRemoteText.Get(running ? "Running" : Config.IsEnabled ? "StartFailed" : "Disabled");
                ServiceStateIndicator.Fill = running ? Brushes.SeaGreen : Config.IsEnabled ? Brushes.DarkOrange : (Brush)FindResource("GlobalTextBrush");
                ServiceStateIndicator.Opacity = running || Config.IsEnabled ? 1 : .4;
                var claims = Service.OperationsHost.GetPendingClaims();
                _pairingClaimed |= claims.Any(item => item.PairingId == _pairingChallenge?.PairingId);
                SetItems(PendingDevicesListBox, claims, item => item.PairingId);
                SetItems(PairedDevicesListBox, Service.OperationsHost.Registry.GetAll().Where(item => item.IsActive).ToList(), item => item.DeviceId);
                SetItems(LocalCoSignJobsListBox, Service.OperationsHost.WorkStore.GetJobs()
                    .Where(item => item.Status == "awaiting_local_cosign"
                        && OperationsWorkStore.RequiresLocalCoSign(item)).ToList(), item => item.JobId);
                SetItems(SupportRequestsListBox, Service.OperationsHost.WorkStore.GetSupportSessions()
                    .Where(item => item.Status == "awaiting_local_consent" && item.ExpiresAt > DateTimeOffset.UtcNow).ToList(), item => item.SessionId);
                RefreshSelectionState();
                RefreshPairingState();
                DevicesTabHeader.Text = CountedTab("DevicesTab", claims.Count);
                ConfirmationsTabHeader.Text = CountedTab("ConfirmationsTab", LocalCoSignJobsListBox.Items.Count + SupportRequestsListBox.Items.Count);
                PendingEmptyText.Visibility = EmptyVisibility(PendingDevicesListBox);
                PairedEmptyText.Visibility = EmptyVisibility(PairedDevicesListBox);
                JobsEmptyText.Visibility = EmptyVisibility(LocalCoSignJobsListBox);
                SupportEmptyText.Visibility = EmptyVisibility(SupportRequestsListBox);

                IpListTextBlock.Text = addresses.Count == 0
                    ? LanRemoteText.Get("NoAddresses")
                    : LanRemoteText.Format("AvailableAddresses", string.Join(", ", addresses));
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RefreshPairingPayload()
        {
            _pairingChallenge = Service.OperationsHost.IsRunning ? Service.OperationsHost.CreatePairingChallenge(Service.GetSecureBaseUrl()) : null;
            _pairingHostStartedAt = Service.StartedAt;
            _pairingClaimed = false;
            QrImage.Source = _pairingChallenge == null ? null : LanRemoteQrCode.Create(Service.OperationsHost.Pairing.BuildQrPayload(_pairingChallenge));
        }

        private void RefreshPairingState()
        {
            bool running = Service.OperationsHost.IsRunning;
            bool expired = _pairingChallenge == null || _pairingChallenge.ExpiresAt <= DateTimeOffset.UtcNow;
            PairingUnavailableOverlay.Visibility = running && !expired && !_pairingClaimed ? Visibility.Collapsed : Visibility.Visible;
            PairingStateTextBlock.Text = LanRemoteText.Get(!running ? "EnableToPair" : _pairingClaimed ? "PairingSubmitted" : "PairingExpired");
            PairingExpiryTextBlock.Text = running && !expired && !_pairingClaimed
                ? LanRemoteText.Format("PairingExpires", _pairingChallenge!.ExpiresAt.ToLocalTime().ToString("T", CultureInfo.CurrentCulture))
                : LanRemoteText.Get("PairingLifetime");
            ResetTokenButton.IsEnabled = running;
            CopyUrlButton.IsEnabled = running;
        }

        private static Visibility EmptyVisibility(ListBox list) => list.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        private static string CountedTab(string key, int count) => count == 0 ? LanRemoteText.Get(key) : LanRemoteText.Format("TabCount", LanRemoteText.Get(key), count);

        private static void SetItems<T>(ListBox list, IReadOnlyList<T> items, Func<T, string> key)
        {
            string? selectedKey = list.SelectedItem is T selected ? key(selected) : null;
            list.ItemsSource = items;
            if (selectedKey != null)
                list.SelectedItem = items.FirstOrDefault(item => key(item) == selectedKey);
        }

        private void ReviewDevicesButton_Click(object sender, RoutedEventArgs e) => SectionsTabControl.SelectedIndex = 1;
        private void Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isRefreshing && IsInitialized) RefreshSelectionState();
        }

        private void RefreshSelectionState()
        {
            ApproveDeviceButton.IsEnabled = RejectDeviceButton.IsEnabled = PendingDevicesListBox.SelectedItem != null;
            RevokeDeviceButton.IsEnabled = PairedDevicesListBox.SelectedItem != null;
            LocalCoSignJobButton.IsEnabled = LocalRejectJobButton.IsEnabled = LocalCoSignJobsListBox.SelectedItem != null;
            ConsentSupportButton.IsEnabled = RejectSupportButton.IsEnabled = SupportRequestsListBox.SelectedItem != null;
        }

        private void Control_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool compact = e.NewSize.Width < 720;
            ArrangeCards(ConnectionGrid, PairingCard, compact);
            ArrangeCards(DevicesGrid, PairedDevicesCard, compact);
            ArrangeCards(ConfirmationsGrid, SupportCard, compact);
        }

        private static void ArrangeCards(Grid grid, Border secondCard, bool compact)
        {
            grid.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 16);
            grid.ColumnDefinitions[2].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(secondCard, compact ? 0 : 2);
            Grid.SetRow(secondCard, compact ? 1 : 0);
            secondCard.Margin = compact ? new Thickness(0, 12, 0, 0) : new Thickness(0);
        }

        private void RefreshIpAddressOptions(IReadOnlyList<string> addresses)
        {
            string selectedAddress = string.IsNullOrWhiteSpace(Config.PreferredHost)
                ? AutoAddressValue
                : Config.PreferredHost;

            IpAddressComboBox.Items.Clear();
            string autoText = addresses.Count > 0
                ? LanRemoteText.Format("AutomaticAddress", addresses[0])
                : LanRemoteText.Get("Automatic");
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
