using ColorVision.Core;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal sealed record LocalCalibrationCacheReleaseFailure(string DeviceCode, string Message);

    internal sealed record LocalCalibrationCacheReleaseSummary(
        int DeviceCount,
        int ContextsReleased,
        CalibrationSharedCacheReleaseResult? NativeRelease,
        IReadOnlyList<LocalCalibrationCacheReleaseFailure> Errors)
    {
        public bool Succeeded => Errors.Count == 0 && NativeRelease != null;
    }

    /// <summary>
    /// Coordinates the two cache ownership layers for the management UI.
    /// Device contexts are released first (each manager waits for its in-flight
    /// execution), then the process-wide native asset references are removed.
    /// </summary>
    internal static class LocalCalibrationCacheService
    {
        public static CalibrationSharedCacheSnapshot GetSnapshot()
            => LocalCalibrationCacheManager.GetEntries();

        public static Task<LocalCalibrationCacheReleaseSummary> ReleaseAllAsync()
            => ReleaseAllAsync(ServiceManager.GetInstance());

        internal static Task<LocalCalibrationCacheReleaseSummary> ReleaseAllAsync(ServiceManager serviceManager)
        {
            ArgumentNullException.ThrowIfNull(serviceManager);
            DeviceCamera[] cameras = serviceManager.DeviceServices.OfType<DeviceCamera>().Distinct().ToArray();
            return ReleaseAllAsync(cameras);
        }

        internal static Task<LocalCalibrationCacheReleaseSummary> ReleaseAllAsync(
            IReadOnlyList<DeviceCamera> cameras)
        {
            ArgumentNullException.ThrowIfNull(cameras);
            return Task.Run(() => LocalCalibrationCacheManager.RunWithExclusiveSharedCacheAccess(
                () => ReleaseAll(cameras)));
        }

        private static LocalCalibrationCacheReleaseSummary ReleaseAll(IReadOnlyList<DeviceCamera> cameras)
        {
            int contextsReleased = 0;
            List<LocalCalibrationCacheReleaseFailure> errors = new();
            foreach (DeviceCamera camera in cameras)
            {
                try
                {
                    contextsReleased = checked(
                        contextsReleased + camera.LocalCalibrationCacheManager.ReleaseCache());
                }
                catch (Exception ex)
                {
                    errors.Add(new LocalCalibrationCacheReleaseFailure(camera.Code, ex.Message));
                }
            }

            CalibrationSharedCacheReleaseResult? nativeRelease = null;
            try
            {
                nativeRelease = LocalCalibrationCacheManager.ClearShared();
            }
            catch (Exception ex)
            {
                errors.Add(new LocalCalibrationCacheReleaseFailure("opencv_helper", ex.Message));
            }

            return new LocalCalibrationCacheReleaseSummary(
                cameras.Count,
                contextsReleased,
                nativeRelease,
                errors.AsReadOnly());
        }
    }
}
