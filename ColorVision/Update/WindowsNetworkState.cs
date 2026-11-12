using ColorVision.UI.Marketplace;
using log4net;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ColorVision.Update
{
    internal static class WindowsNetworkState
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowsNetworkState));

        private static readonly TimeSpan HostResolutionTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Whether an update check is worth attempting. Windows reports "no Internet" on isolated networks even when
        /// the update service is reachable through an internal relay or mirror, so local connectivity suffices when a
        /// legacy hosts redirect resolves the official name to a private address. Explicit sources are always tried.
        /// </summary>
        public static bool CanReachUpdateService()
        {
            if (!MarketplaceConfig.UsesOnlyDefaultServiceBaseUrl || IsConnectedToInternet())
                return true;
            if (!IsConnectedToAnyNetwork())
                return false;
            return ResolvesToPrivateAddress(MarketplaceConfig.DefaultServiceBaseUrl);
        }

        private static bool IsConnectedToAnyNetwork()
        {
            try
            {
                var manager = (INetworkListManager)new NetworkListManager();
                return manager.IsConnected;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to query Windows network connectivity: {ex.GetBaseException().Message}");
                return true;
            }
        }

        private static bool ResolvesToPrivateAddress(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
                return false;
            if (IPAddress.TryParse(uri.Host, out IPAddress? literal))
                return IsPrivate(literal);
            try
            {
                Task<IPAddress[]> lookup = Dns.GetHostAddressesAsync(uri.Host);
                return lookup.Wait(HostResolutionTimeout) && lookup.Result.Any(IsPrivate);
            }
            catch (Exception ex)
            {
                log.Info($"Could not resolve {uri.Host} on the local network: {ex.GetBaseException().Message}");
                return false;
            }
        }

        internal static bool IsPrivate(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;
            byte[] b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }

        public static bool IsConnectedToInternet()
        {
            try
            {
                var manager = (INetworkListManager)new NetworkListManager();
                return manager.IsConnectedToInternet;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to query Windows network connectivity: {ex.GetBaseException().Message}");
                return true;
            }
        }

        [ComImport]
        [Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B")]
        private class NetworkListManager
        {
        }

        [ComImport]
        [Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        private interface INetworkListManager
        {
            object GetNetworks(int flags);

            object GetNetwork(Guid networkId);

            object GetNetworkConnections();

            object GetNetworkConnection(Guid networkConnectionId);

            bool IsConnectedToInternet { get; }

            bool IsConnected { get; }

            int GetConnectivity();
        }
    }
}
