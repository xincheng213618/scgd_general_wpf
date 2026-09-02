using ColorVision.UI.ServiceHost;
using log4net;
using System;
using System.Threading.Tasks;

namespace ColorVision.Update.Export
{
    public static class FileAssociationHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FileAssociationHelper));

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        public static async Task<bool> RegisterAssociationsAsync()
        {
            try
            {
                string appPath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to resolve executable path.");
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .RegisterFileAssociationsAsync(appPath)
                    .ConfigureAwait(false);

                if (!response.Success)
                {
                    log.Warn($"RegisterAssociations failed: {response.ToDisplayText()}");
                    return false;
                }

                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                log.Info("RegisterAssociations completed through ColorVisionServiceHost.");
                return true;
            }
            catch (Exception ex)
            {
                log.Error("RegisterAssociations failed.", ex);
                return false;
            }
        }
    }
}
