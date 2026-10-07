using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Core;
using FlowEngineLib.Algorithm;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Owns the reusable native contexts used by process-local calibration nodes.
    /// Image frames are owned separately by <see cref="LocalFlowFrame"/>.
    /// </summary>
    internal sealed class LocalCalibrationCacheManager : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalCalibrationCacheManager));
        private static readonly ReaderWriterLockSlim SharedCacheLifecycleGate = new(LockRecursionPolicy.NoRecursion);
        private readonly string deviceCode;
        private readonly SemaphoreSlim openCvGate = new(1, 1);
        private readonly OpenCvLocalCalibrationCache openCvCache = new();
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

        public int CachedItemCount
        {
            get
            {
                openCvGate.Wait();
                try
                {
                    return openCvCache.CachedItemCount;
                }
                finally
                {
                    openCvGate.Release();
                }
            }
        }

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

            SemaphoreSlim executionGate = FlowNodeTiming.Run("WaitCalibration", EnterExecution);
            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return openCvCache.Execute(layout, calibrationFiles, rawPointer, ciePointer, exposure, calibrationRoi, allowAcceleration, rawOutputFlip);
            }
            finally
            {
                ExitExecution(executionGate);
            }
        }

        public int ReleaseCache()
        {
            openCvGate.Wait();
            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return openCvCache.Release();
            }
            finally
            {
                openCvGate.Release();
            }
        }

        public Task<int> ReleaseCacheAsync() => Task.Run(ReleaseCache);

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
            openCvGate.Wait();
            try
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    openCvCache.Dispose();
                }
                catch (Exception ex)
                {
                    log.Error($"Release local calibration cache failed: {deviceCode}", ex);
                }
            }
            finally
            {
                openCvGate.Release();
            }
        }

        private SemaphoreSlim EnterExecution()
        {
            SharedCacheLifecycleGate.EnterReadLock();
            try
            {
                openCvGate.Wait();
                return openCvGate;
            }
            catch
            {
                SharedCacheLifecycleGate.ExitReadLock();
                throw;
            }
        }

        private static void ExitExecution(SemaphoreSlim executionGate)
        {
            try
            {
                executionGate.Release();
            }
            finally
            {
                SharedCacheLifecycleGate.ExitReadLock();
            }
        }
    }

    internal readonly record struct LocalCalibrationLayout(int Width, int Height, int Bpp, int Channels);
}
