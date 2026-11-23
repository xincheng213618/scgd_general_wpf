using ColorVision.UI;
using ColorVision.UI.Marketplace;
using ColorVision.Update;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ToolPlugins.UpdateRelay;

internal static class UpdateRelayConfiguration
{
    private static readonly Uri OfficialSource = new(MarketplaceConfig.DefaultServiceBaseUrl);

    internal static int DefaultPort => OfficialSource.Port;
    internal static string OfficialHost => OfficialSource.Host;

    internal static string GetClientUrl(string address, int port)
    {
        address = address.Trim();
        if (!IPAddress.TryParse(address, out IPAddress? ip) || !IsUsableIPv4(ip) || ip.ToString() != address)
            throw new ArgumentException("请填写可用的跳板机 IPv4 地址，例如 192.168.1.20。", nameof(address));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "端口必须在 1 到 65535 之间。");
        return new UriBuilder(Uri.UriSchemeHttp, ip.ToString(), port).Uri.GetLeftPart(UriPartial.Authority);
    }

    internal static bool AddClientSource(string url)
    {
        if (GetLocalAddresses().Contains(new Uri(url).Host, StringComparer.Ordinal))
            throw new InvalidOperationException("请输入另一台电脑的跳板机地址。本机已开启转发时，可以在跳板机页测试。");
        return UpdateClientSource(url, add: true);
    }

    internal static bool RemoveClientSource(string url) => UpdateClientSource(url, add: false);

    private static bool UpdateClientSource(string url, bool add)
    {
        UpdateNetworkConfig config = UpdateNetworkConfig.Instance;
        string? setting = BuildClientSourceSetting(MarketplaceConfig.ServiceBaseUrls, url, add);
        if (setting == null)
            return false;

        string previousSetting = config.UpdateServerUrls;
        string previousSource = MarketplaceConfig.UpdateSourceUrl;
        config.UpdateServerUrls = setting;
        if (ConfigHandler.GetInstance().TrySave(config, out string error))
            return true;

        config.UpdateServerUrls = previousSetting;
        MarketplaceConfig.SetActiveServiceBaseUrl(previousSource);
        throw new InvalidOperationException($"保存更新源失败。{error}");
    }

    internal static string? BuildClientSourceSetting(IReadOnlyList<string> sources, string url, bool add)
    {
        bool contains = sources.Contains(url, StringComparer.OrdinalIgnoreCase);
        if (add ? contains : !contains)
            return null;

        IEnumerable<string> updatedSources = add
            ? sources.Append(url)
            : sources.Where(source => !string.Equals(source, url, StringComparison.OrdinalIgnoreCase));
        return string.Join("; ", updatedSources);
    }

    internal static IReadOnlyList<string> GetLocalAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
        .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
        .Select(address => address.Address)
        .Where(IsUsableIPv4)
        .Select(address => address.ToString())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    internal static async Task<string> TestSourceAsync(string url, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using HttpResponseMessage response = await UpdateHttpClientProvider.GetClient()
                .GetAsync($"{url.TrimEnd('/')}/api/app/latest-version", timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string payload = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (UpdateSourceSelector.TryParseLatestVersion(payload, out Version? version))
                return version.ToString();
            throw new InvalidOperationException("目标没有返回有效的 ColorVision 版本。");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("连接更新服务器超时。");
        }
    }

    private static bool IsUsableIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] is > 0 and < 224 and not 127 && (bytes[0] != 169 || bytes[1] != 254);
    }
}
