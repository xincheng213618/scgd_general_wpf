using log4net;
using System;
using System.Threading.Tasks;

namespace ColorVision.ServiceHost
{
    internal enum ServiceHostStartupAction
    {
        None,
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
                ServiceHostStatus status = await ColorVisionServiceHostManager.QueryStatusAsync().ConfigureAwait(true);
                ServiceHostStartupAction action = ResolveAction(status);
                if (action == ServiceHostStartupAction.None)
                    return;

                if (action == ServiceHostStartupAction.SelfUpdate)
                {
                    ServiceHostOperationResult selfUpdateResult = await ColorVisionServiceHostManager.SelfUpdateAsync().ConfigureAwait(true);
                    if (selfUpdateResult.Success)
                    {
                        log.Info($"ColorVisionServiceHost silent update started: {status.RunningVersion} -> {status.PackageVersion}");
                        return;
                    }

                    log.Warn($"ColorVisionServiceHost silent update failed; falling back to elevated install: {selfUpdateResult.Summary}");
                }

                ServiceHostOperationResult installResult = await ColorVisionServiceHostManager.InstallAsync().ConfigureAwait(true);
                if (installResult.Success)
                {
                    log.Info($"ColorVisionServiceHost elevated update completed: {status.InstalledVersion} -> {status.PackageVersion}");
                    return;
                }

                log.Warn($"ColorVisionServiceHost elevated update failed: {installResult.Summary}");
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

            if (status.NeedsRepair || status.NeedsInstall)
                return ServiceHostStartupAction.InstallOrRepair;

            if (!status.NeedsUpdate)
                return ServiceHostStartupAction.None;

            return status.CanSelfUpdate ? ServiceHostStartupAction.SelfUpdate : ServiceHostStartupAction.InstallOrRepair;
        }
    }
}
