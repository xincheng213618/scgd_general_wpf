using ColorVision.UI.ServiceHost;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.PhyCameras
{
    internal sealed class PhysicalCameraCreationBatch
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(PhysicalCameraCreationBatch));
        private static readonly SemaphoreSlim RestartGate = new(1, 1);
        private readonly HashSet<string> cameraCodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, Task<ServiceHostResponse>> restartService;
        private readonly object sync = new();
        private Task<bool>? activationTask;

        internal PhysicalCameraCreationBatch(Func<string, Task<ServiceHostResponse>>? restartService = null)
        {
            this.restartService = restartService ?? (name => ColorVisionServiceHostClient.Default.RestartServiceAsync(name));
        }

        internal void RecordSaved(bool requiresCreation, SysResourceModel resource, int saveResult)
        {
            // Positive IDs belong to MySQL; offline configurations never need a Windows service restart.
            if (requiresCreation && saveResult > 0 && resource.Id > 0 && !string.IsNullOrWhiteSpace(resource.Code))
            {
                cameraCodes.Add(resource.Code);
            }
        }

        internal Task<bool> ActivateAsync()
        {
            lock (sync)
            {
                return activationTask ??= ActivateCoreAsync();
            }
        }

        private async Task<bool> ActivateCoreAsync()
        {
            if (cameraCodes.Count == 0) return true;

            await RestartGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Restarting RC rebuilds its file resource list without clearing camera directories.
                // The MQTT "restart devices" message does not rebuild that list.
                log.Info($"Activating new physical cameras through RegistrationCenterService: {string.Join(", ", cameraCodes)}");
                ServiceHostResponse response = await restartService("RegistrationCenterService").ConfigureAwait(false);
                if (response.Success)
                    log.Info("RegistrationCenterService restarted after physical camera creation.");
                else
                    log.Error($"Physical camera configuration was saved, but RegistrationCenterService restart failed: {response.Message}");
                return response.Success;
            }
            catch (Exception ex)
            {
                log.Error("Physical camera configuration was saved, but RegistrationCenterService restart failed. Check ColorVisionServiceHost and the local registration service.", ex);
                return false;
            }
            finally
            {
                RestartGate.Release();
            }
        }
    }
}
