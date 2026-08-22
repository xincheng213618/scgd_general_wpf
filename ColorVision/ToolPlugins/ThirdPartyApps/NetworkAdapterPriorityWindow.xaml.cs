using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ToolPlugins.ThirdPartyApps
{
    public partial class NetworkAdapterPriorityWindow : Window
    {
        private bool _isBusy;

        public NetworkAdapterPriorityWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            UpdateActionButtons();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAdaptersAsync().ConfigureAwait(true);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAdaptersAsync().ConfigureAwait(true);
        }

        private async void SetPreferredButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdaptersGrid.SelectedItem is not NetworkAdapterInfo adapter)
                return;

            MessageBoxResult result = MessageBox.Show(
                this,
                $"将“{adapter.InterfaceAlias}”的 IPv4 自动 Metric 关闭并设为 {NetworkAdapterPriorityService.PreferredMetric}。\n\n此操作只修改所选网卡，是否继续？",
                "设置首选上网网卡",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            await RunAdapterChangeAsync(
                () => NetworkAdapterPriorityService.SetPreferredAsync(adapter.InterfaceIndex),
                $"已将“{adapter.InterfaceAlias}”设为首选（Metric {NetworkAdapterPriorityService.PreferredMetric}）。",
                adapter.InterfaceIndex).ConfigureAwait(true);
        }

        private async void RestoreAutomaticButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdaptersGrid.SelectedItem is not NetworkAdapterInfo adapter)
                return;

            MessageBoxResult result = MessageBox.Show(
                this,
                $"恢复“{adapter.InterfaceAlias}”的 IPv4 自动 Metric？",
                "恢复自动 Metric",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            await RunAdapterChangeAsync(
                () => NetworkAdapterPriorityService.RestoreAutomaticMetricAsync(adapter.InterfaceIndex),
                $"已恢复“{adapter.InterfaceAlias}”的自动 Metric。",
                adapter.InterfaceIndex).ConfigureAwait(true);
        }

        private async void SetDnsAndFlushButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdaptersGrid.SelectedItem is not NetworkAdapterInfo adapter)
                return;

            MessageBoxResult result = MessageBox.Show(
                this,
                $"将“{adapter.InterfaceAlias}”的 IPv4 DNS 设置为 {NetworkAdapterPriorityService.PreferredDnsServer}，然后刷新 Windows DNS 缓存。\n\n此操作会替换该网卡现有的手动 DNS 列表，是否继续？",
                "设置 DNS 并刷新缓存",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            await RunAdapterChangeAsync(
                () => NetworkAdapterPriorityService.SetDnsAndFlushAsync(adapter.InterfaceIndex),
                $"已将“{adapter.InterfaceAlias}”的 DNS 设置为 {NetworkAdapterPriorityService.PreferredDnsServer}，并刷新 DNS 缓存。",
                adapter.InterfaceIndex).ConfigureAwait(true);
        }

        private void AdaptersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateActionButtons();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task RefreshAdaptersAsync(string? successMessage = null, int? selectedInterfaceIndex = null)
        {
            SetBusy(true, "正在读取 IPv4 网卡、DNS 和默认路由……");
            try
            {
                IReadOnlyList<NetworkAdapterInfo> adapters = await NetworkAdapterPriorityService.GetAdaptersAsync().ConfigureAwait(true);
                AdaptersGrid.ItemsSource = adapters;
                if (adapters.Count > 0)
                {
                    AdaptersGrid.SelectedItem = selectedInterfaceIndex.HasValue
                        ? adapters.FirstOrDefault(adapter => adapter.InterfaceIndex == selectedInterfaceIndex.Value) ?? adapters[0]
                        : adapters[0];
                }

                StatusText.Text = successMessage ?? $"已读取 {adapters.Count} 个 IPv4 接口。Metric 数值越小，优先级通常越高。";
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "读取网卡失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task RunAdapterChangeAsync(Func<Task> changeAction, string successMessage, int selectedInterfaceIndex)
        {
            SetBusy(true, "正在等待管理员授权并修改网卡设置……");
            try
            {
                await changeAction().ConfigureAwait(true);
                await RefreshAdaptersAsync(successMessage, selectedInterfaceIndex).ConfigureAwait(true);
            }
            catch (OperationCanceledException ex)
            {
                StatusText.Text = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "修改网卡失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool isBusy, string? status = null)
        {
            _isBusy = isBusy;
            RefreshButton.IsEnabled = !isBusy;
            AdaptersGrid.IsEnabled = !isBusy;
            CloseButton.IsEnabled = !isBusy;
            if (!string.IsNullOrEmpty(status))
                StatusText.Text = status;
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool hasSelection = AdaptersGrid.SelectedItem is NetworkAdapterInfo;
            SetPreferredButton.IsEnabled = !_isBusy && hasSelection;
            RestoreAutomaticButton.IsEnabled = !_isBusy && hasSelection;
            SetDnsAndFlushButton.IsEnabled = !_isBusy && hasSelection;
        }
    }
}
