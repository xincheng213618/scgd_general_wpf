using log4net;
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Update
{
    internal static class WindowsNetworkState
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowsNetworkState));

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
