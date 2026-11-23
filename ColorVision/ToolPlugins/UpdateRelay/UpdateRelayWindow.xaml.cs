using ColorVision.Common.ThirdPartyApps;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.ToolPlugins.ThirdPartyApps;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Marketplace;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ToolPlugins.UpdateRelay;

public sealed class UpdateRelayAppProvider : IThirdPartyAppProvider
{
    internal const string ActionId = "tools.update-relay.open";
    internal const string ResourceId = "update.relay";

    public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
    {
        yield return new ThirdPartyAppInfo
        {
            Name = DisplayText.Get("更新跳板"),
            Group = ThirdPartyAppGroupNames.CommonTools,
            Category = ThirdPartyAppCategory.Internal,
            RequiredPermission = PermissionMode.Administrator,
            ActionId = ActionId,
            ResourceId = ResourceId,
            Order = -896,
            IconGlyph = ThirdPartyAppIconGlyphs.NetworkAdapter,
            LaunchAction = UpdateRelayWindow.ShowWindow
        };
    }
}

public partial class UpdateRelayWindow : Window
{
    private readonly CancellationTokenSource _lifetime = new();
    private bool _closed;

    public UpdateRelayWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
    }

    internal static void ShowWindow()
    {
        AccessControl.Demand(UpdateRelayAppProvider.ActionId, PermissionMode.Administrator, UpdateRelayAppProvider.ResourceId);
        if (Application.Current.Windows.OfType<UpdateRelayWindow>().FirstOrDefault() is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }
        new UpdateRelayWindow { Owner = Application.Current.GetActiveWindow() }.Show();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshClientAddresses();
        RefreshMachineState();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _closed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private void RefreshClientAddresses()
    {
        string text = ClientAddress.Text;
        string[] addresses = MarketplaceConfig.ServiceBaseUrls
            .Select(source => new Uri(source))
            .Where(uri => uri.Scheme == Uri.UriSchemeHttp && uri.HostNameType == UriHostNameType.IPv4 && uri.AbsolutePath == "/")
            .Select(uri => uri.Port == UpdateRelayConfiguration.DefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}")
            .ToArray();
        ClientAddress.ItemsSource = addresses;
        ClientAddress.Text = string.IsNullOrWhiteSpace(text) ? addresses.FirstOrDefault() ?? string.Empty : text;
    }

    private void RefreshMachineState()
    {
        try
        {
            string? selected = LocalAddress.SelectedItem as string;
            IReadOnlyList<string> addresses = UpdateRelayConfiguration.GetLocalAddresses();
            LocalAddress.ItemsSource = addresses;
            LocalAddress.SelectedItem = selected != null && addresses.Contains(selected) ? selected : addresses.FirstOrDefault();
            ForwardStatus.Text = UpdateRelayForwarding.IsConfigured()
                ? DisplayText.Format($"已配置 TCP {UpdateRelayConfiguration.DefaultPort} 转发")
                : DisplayText.Format($"未配置 TCP {UpdateRelayConfiguration.DefaultPort} 转发");
        }
        catch (Exception ex)
        {
            ForwardStatus.Text = DisplayText.Format($"读取转发状态失败：{ex.Message}");
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshMachineState();

    private void CopyAddress_Click(object sender, RoutedEventArgs e) => RunLocalAction(() =>
    {
        string address = GetSelectedLocalAddress();
        Clipboard.SetText(address);
        return DisplayText.Format($"已复制本机地址 {address}");
    });

    private void AddSource_Click(object sender, RoutedEventArgs e) => RunLocalAction(() =>
    {
        string url = GetEnteredClientUrl();
        bool added = UpdateRelayConfiguration.AddClientSource(url);
        RefreshClientAddresses();
        return added ? DisplayText.Format($"已添加更新源 {url}") : DisplayText.Format($"更新源列表已包含 {url}");
    });

    private void RemoveSource_Click(object sender, RoutedEventArgs e) => RunLocalAction(() =>
    {
        string url = GetEnteredClientUrl();
        bool removed = UpdateRelayConfiguration.RemoveClientSource(url);
        RefreshClientAddresses();
        return removed ? DisplayText.Format($"已移除更新源 {url}") : DisplayText.Format($"更新源列表没有 {url}");
    });

    private async void TestSource_Click(object sender, RoutedEventArgs e) => await RunAsync(() => TestConnectionAsync(GetEnteredClientUrl()));

    private async void TestForward_Click(object sender, RoutedEventArgs e) => await RunAsync(() =>
        TestConnectionAsync(UpdateRelayConfiguration.GetClientUrl(GetSelectedLocalAddress(), UpdateRelayConfiguration.DefaultPort)));

    private async void EnableForward_Click(object sender, RoutedEventArgs e) => await RunAsync(() => ChangeForwardAsync(enable: true));

    private async void DisableForward_Click(object sender, RoutedEventArgs e) => await RunAsync(() => ChangeForwardAsync(enable: false));

    private string GetEnteredClientUrl()
    {
        string[] parts = ClientAddress.Text.Split(':', 2, StringSplitOptions.TrimEntries);
        int port = UpdateRelayConfiguration.DefaultPort;
        if (parts.Length == 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out port))
            throw new ArgumentException(DisplayText.Get("请输入有效的端口号。"));
        return UpdateRelayConfiguration.GetClientUrl(parts[0], port);
    }

    private string GetSelectedLocalAddress() => LocalAddress.SelectedItem as string
        ?? throw new InvalidOperationException(DisplayText.Get("未找到可用的本机 IPv4 地址。"));

    private async Task<string> TestConnectionAsync(string url)
    {
        string version = await UpdateRelayConfiguration.TestSourceAsync(url, _lifetime.Token);
        return DisplayText.Format($"连接正常，更新服务版本 {version}");
    }

    private async Task<string> ChangeForwardAsync(bool enable)
    {
        AccessControl.Demand(UpdateRelayAppProvider.ActionId, PermissionMode.Administrator, UpdateRelayAppProvider.ResourceId);
        try
        {
            if (Tool.IsAdministrator())
            {
                await Task.Run(() => UpdateRelayForwarding.Configure(enable));
            }
            else
            {
                string executable = Path.Combine(AppContext.BaseDirectory, "ColorVision.exe");
                var startInfo = new ProcessStartInfo(executable)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Environment.SystemDirectory,
                    ArgumentList = { UpdateRelayForwarding.StartupArgument, enable ? "enable" : "disable" }
                };
                using Process process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException(DisplayText.Get("无法启动更新跳板配置。"));
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    return DisplayText.Format($"转发配置失败（退出码 {process.ExitCode}）。");
            }

            return DisplayText.Get(enable ? "已开启转发。可测试连接后复制地址。" : "已移除转发。");
        }
        finally
        {
            if (!_closed) RefreshMachineState();
        }
    }

    private void RunLocalAction(Func<string> action)
    {
        try { OperationStatus.Text = action(); }
        catch (Exception ex) { OperationStatus.Text = ex.Message; }
    }

    private async Task RunAsync(Func<Task<string>> action)
    {
        RelayTabs.IsEnabled = false;
        OperationStatus.Text = DisplayText.Get("正在处理…");
        try
        {
            string result = await action();
            if (!_closed) OperationStatus.Text = result;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            if (!_closed) OperationStatus.Text = DisplayText.Get("已取消管理员授权。");
        }
        catch (Exception ex)
        {
            if (!_closed) OperationStatus.Text = ex.Message;
        }
        finally
        {
            if (!_closed) RelayTabs.IsEnabled = true;
        }
    }
}
