using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using FlowEngineLib.Algorithm;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Serializes one device's opencv_helper calibration cache and its lifetime.
    /// Process-wide maintenance excludes execution across all devices; image
    /// frames remain owned separately by <see cref="LocalFlowFrame"/>.
    /// </summary>
    internal sealed class LocalCalibrationCacheManager : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalCalibrationCacheManager));
        private static readonly ReaderWriterLockSlim SharedCacheLifecycleGate = new(LockRecursionPolicy.NoRecursion);
        private readonly string deviceCode;
        private readonly Lock executionGate = new();
        private readonly OpenCvLocalCalibrationCache cache = new();
        private bool disposed;

        public LocalCalibrationCacheManager(string deviceCode)
        {
            this.deviceCode = deviceCode;
        }

        public string BackendName => "opencv_helper";

        /// <summary>
        /// Returns a coherent snapshot of the process-wide immutable native
        /// calibration assets. Per-device contexts are reported as owners.
        /// </summary>
        public static CalibrationSharedCacheSnapshot GetEntries()
            => OpenCVCalibration.GetCalibrationSharedCacheEntries();

        /// <summary>
        /// Drops the native cache's process-wide strong references. Callers
        /// that intend to release memory should first release every device's
        /// context LRU so those contexts no longer own the assets.
        /// </summary>
        public static CalibrationSharedCacheReleaseResult ClearShared()
            => OpenCVCalibration.ClearCalibrationSharedCache();

        public RawColorTransformV1? Execute(
            LocalCalibrationLayout layout,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            IntPtr rawPointer,
            IntPtr ciePointer,
            float[] exposure,
            LocalCalibrationRoi calibrationRoi,
            bool allowAcceleration = false,
            CVImageFlipMode rawOutputFlip = CVImageFlipMode.None)
        {
            ArgumentNullException.ThrowIfNull(calibrationFiles);
            ArgumentNullException.ThrowIfNull(exposure);
            if (rawPointer == IntPtr.Zero) throw new ArgumentException("RAW 指针为空。", nameof(rawPointer));

            FlowNodeTiming.Run("WaitCalibration", EnterExecution);
            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return cache.Execute(layout, calibrationFiles, rawPointer, ciePointer, exposure, calibrationRoi, allowAcceleration, rawOutputFlip);
            }
            finally
            {
                ExitExecution();
            }
        }

        public int ReleaseCache()
        {
            lock (executionGate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return cache.Release();
            }
        }

        /// <summary>
        /// Prevents new opencv_helper executions while a process-wide cache
        /// maintenance operation waits for and releases every device context.
        /// Existing executions use shared/read access and finish normally.
        /// </summary>
        internal static T RunWithExclusiveSharedCacheAccess<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            SharedCacheLifecycleGate.EnterWriteLock();
            try
            {
                return action();
            }
            finally
            {
                SharedCacheLifecycleGate.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            lock (executionGate)
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    cache.Dispose();
                }
                catch (Exception ex)
                {
                    log.Error($"Release local calibration cache failed: {deviceCode}", ex);
                }
            }
        }

        private void EnterExecution()
        {
            // Always acquire the process gate before the device gate. Maintenance
            // holds the write lock while releasing each device's cache.
            SharedCacheLifecycleGate.EnterReadLock();
            try
            {
                executionGate.Enter();
            }
            catch
            {
                SharedCacheLifecycleGate.ExitReadLock();
                throw;
            }
        }

        private void ExitExecution()
        {
            try
            {
                executionGate.Exit();
            }
            finally
            {
                SharedCacheLifecycleGate.ExitReadLock();
            }
        }
    }

    internal readonly record struct LocalCalibrationLayout(int Width, int Height, int Bpp, int Channels);
}
