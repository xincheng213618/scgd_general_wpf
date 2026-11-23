using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using FlowEngineLib.Algorithm;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public LocalPreparedCalibration Prepare(IReadOnlyList<DeviceCameraCalibrationFile> files, string templateName)
        {
            ArgumentNullException.ThrowIfNull(files);
            var selection = files.ToArray();
            if (selection.Length == 0)
                throw new InvalidOperationException($"校正模板“{templateName}”没有选择任何校正文件。");
            if (selection.Count(file => LocalFrameCalibrationService.IsColorCalibration(file.CalibrationType)) > 1)
                throw new InvalidOperationException($"校正模板“{templateName}”同时选择了多个亮度/颜色校正文件，本地校正只能使用一个。");
            return WithCache(() =>
            {
                var prepared = FlowNodeTiming.Run("LoadCalibrationResources", () => cache.Prepare(selection));
                return new LocalPreparedCalibration(this, templateName, prepared.Assets, prepared.InputDepth);
            });
        }

        // The keyboard luminance adapter owns a pinned sample array instead of a LocalFlowFrame.
        public LocalCalibrationExecution Execute(
            LocalCalibrationLayout layout,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            IntPtr rawPointer,
            ulong rawLength,
            IntPtr ciePointer,
            ulong cieLength,
            float[] exposure,
            LocalCalibrationRoi calibrationRoi,
            CalibrationOutputMode outputMode,
            CVImageFlipMode rawOutputFlip = CVImageFlipMode.None)
        {
            ArgumentNullException.ThrowIfNull(calibrationFiles);
            ArgumentNullException.ThrowIfNull(exposure);
            if (rawPointer == IntPtr.Zero) throw new ArgumentException("RAW 指针为空。", nameof(rawPointer));

            return WithCache(() => cache.Execute(layout, cache.Prepare(calibrationFiles).Assets, rawPointer, rawLength,
                ciePointer, cieLength, exposure, calibrationRoi, outputMode, rawOutputFlip));
        }

        internal LocalCalibrationExecution ExecutePrepared(LocalCalibrationAsset[] assets, LocalCalibrationLayout layout,
            IntPtr raw, ulong rawLength, IntPtr cie, ulong cieLength, float[] exposure, LocalCalibrationRoi roi,
            CalibrationOutputMode output, CVImageFlipMode flip)
            => WithCache(() => cache.Execute(layout, assets, raw, rawLength, cie, cieLength, exposure, roi, output, flip));

        private T WithCache<T>(Func<T> action)
        {
            FlowNodeTiming.Run("WaitCalibration", EnterExecution);
            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return action();
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

    internal readonly record struct LocalCalibrationLayout(int Width, int Height, int Bpp, int Channels)
    {
        public static LocalCalibrationLayout FromFrame(LocalFrameMetadata metadata)
            => new(metadata.Width, metadata.Height, metadata.SourceBpp, metadata.Channels);
    }
    internal sealed record LocalCalibrationExecution(RawColorTransformV1? Transform,
        ColorVision.ImageEditor.Tif.ImageCalibrationSource[] Sources);
}
