using ColorVision.Common.Utilities;
using log4net;
using System.IO;

namespace ColorVision.UI.ServiceHost
{
    public static class ApplicationUpdatePrivilegeBroker
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationUpdatePrivilegeBroker));

        public static bool TryPrepareApplicationDirectory(string? serviceHostPackageDirectory = null, TimeSpan? timeout = null)
        {
            string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Tool.HasWritePermission(applicationDirectory))
            {
                TryUpdateServiceHost(serviceHostPackageDirectory, timeout);
                log.Debug($"Application directory is already writable; ColorVisionServiceHost access preparation is not required: {applicationDirectory}");
                return true;
            }

            return TryPrepareApplicationDirectory(
                applicationDirectory,
                () => ColorVisionServiceHostClient.Default.PrepareApplicationUpdateAsync(serviceHostPackageDirectory, timeout));
        }

        private static void TryUpdateServiceHost(string? serviceHostPackageDirectory, TimeSpan? timeout)
        {
            if (string.IsNullOrWhiteSpace(serviceHostPackageDirectory))
                return;

            try
            {
                ServiceHostResponse response = ColorVisionServiceHostClient.Default
                    .SelfUpdateAsync(serviceHostPackageDirectory, timeout ?? TimeSpan.FromSeconds(10))
                    .GetAwaiter()
                    .GetResult();
                if (!response.Success)
                    log.Info($"ColorVisionServiceHost self update was skipped: {response.Message}");
            }
            catch (Exception ex)
            {
                log.Info("ColorVisionServiceHost self update was unavailable; the application update will continue.", ex);
            }
        }

        internal static bool TryPrepareApplicationDirectory(string applicationDirectory, Func<Task<ServiceHostResponse>> prepareAccessAsync)
        {
            if (Tool.HasWritePermission(applicationDirectory))
            {
                log.Debug($"Application directory is already writable; ColorVisionServiceHost is not required: {applicationDirectory}");
                return true;
            }

            return TryPrepareApplicationDirectory(prepareAccessAsync);
        }

        internal static bool TryPrepareApplicationDirectory(Func<Task<ServiceHostResponse>> prepareAccessAsync)
        {
            try
            {
                ServiceHostResponse response = prepareAccessAsync().GetAwaiter().GetResult();
                if (!response.Success)
                {
                    log.Info($"ColorVisionServiceHost did not prepare application update access: {response.Message}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Info("ColorVisionServiceHost is unavailable for application update preparation.", ex);
                return false;
            }
        }
    }
}
