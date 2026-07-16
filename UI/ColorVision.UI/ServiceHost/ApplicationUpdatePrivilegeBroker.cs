using log4net;

namespace ColorVision.UI.ServiceHost
{
    public static class ApplicationUpdatePrivilegeBroker
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationUpdatePrivilegeBroker));

        public static bool TryPrepareApplicationDirectory(string? serviceHostPackageDirectory = null, TimeSpan? timeout = null)
        {
            return TryPrepareApplicationDirectory(
                () => ColorVisionServiceHostClient.Default.PrepareApplicationUpdateAsync(serviceHostPackageDirectory, timeout));
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
