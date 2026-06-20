using ColorVision.Common.Utilities;
using ColorVision.UI.Marketplace;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Desktop.LanRemote
{
    public partial class LanRemoteControlSettingsControl : UserControl
    {
        private bool _isRefreshing;

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
            Config.ResetPairingToken();
            SaveAndApply();
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

                var addresses = LanRemoteControlService.GetLocalIpAddresses();
                RefreshIpAddressOptions(addresses);

                string appDownloadUrl = GetAppDownloadUrl();
                AppDownloadUrlTextBox.Text = appDownloadUrl;
                AppDownloadQrImage.Source = LanRemoteQrCode.Create(appDownloadUrl);

                string connectionUrl = Service.GetConnectionUrl();
                ConnectionUrlTextBox.Text = connectionUrl;
                QrImage.Source = LanRemoteQrCode.Create(connectionUrl);
                QrCard.Opacity = Config.IsEnabled ? 1 : 0.38;

                StatusTextBlock.Text = Service.LastStatusMessage;
                ServiceStateTextBlock.Text = Service.IsRunning ? "运行中" : Config.IsEnabled ? "启动失败" : "未启用";

                IpListTextBlock.Text = addresses.Count == 0
                    ? "未检测到可用的局域网 IPv4 地址，请确认电脑和手机在同一个 Wi-Fi 或网段。"
                    : $"可用地址：{string.Join(", ", addresses)}";
            }
            finally
            {
                _isRefreshing = false;
            }
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
