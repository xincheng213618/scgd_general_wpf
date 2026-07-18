using log4net;
using System;
using System.Threading.Tasks;

namespace ColorVision.ServiceHost
{
    internal enum ServiceHostStartupAction
    {
        None,
        Start,
        SelfUpdate,
        InstallOrRepair,
    }

    internal static class ServiceHostStartupUpdateChecker
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceHostStartupUpdateChecker));
        private static bool _isChecking;

        public static async Task CheckAndUpdateAsync()
        {
            if (_isChecking)
                return;

            _isChecking = true;
            try
            {
                ServiceHostEnsureResult result = await ColorVisionServiceHostManager.EnsureReadyAsync().ConfigureAwait(true);
                if (result.Success)
                    log.Info(result.Summary);
                else
                    log.Warn($"ColorVisionServiceHost startup readiness failed: {result.Summary}");
            }
            catch (Exception ex)
            {
                log.Warn("Service host startup update check failed.", ex);
            }
            finally
            {
                _isChecking = false;
            }
        }

        internal static ServiceHostStartupAction ResolveAction(ServiceHostStatus status)
        {
            if (!status.IsPackageAvailable || status.PackageVersion == null)
                return ServiceHostStartupAction.None;

            if (status.State == ServiceHostInstallState.Stopped && status.HasCurrentOrNewerInstalledVersion)
                return ServiceHostStartupAction.Start;

            if (status.WouldInstallDowngrade)
                return ServiceHostStartupAction.None;

            if (status.NeedsUpdate)
                return status.CanSelfUpdate ? ServiceHostStartupAction.SelfUpdate : ServiceHostStartupAction.InstallOrRepair;

            if (ColorVisionServiceHostManager.IsReadyForPackagedVersion(status))
                return ServiceHostStartupAction.None;

            return ServiceHostStartupAction.InstallOrRepair;
        }
    }
}
