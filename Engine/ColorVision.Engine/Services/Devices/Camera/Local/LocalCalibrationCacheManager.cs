using ColorVision.Core;
using cvColorVision;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        private static readonly SemaphoreSlim LegacyNativeGate = new(1, 1);
        private static readonly ReaderWriterLockSlim SharedCacheLifecycleGate = new(LockRecursionPolicy.NoRecursion);
        private readonly string deviceCode;
        private readonly bool useLegacyCalibration;
        private readonly SemaphoreSlim openCvGate = new(1, 1);
        private readonly OpenCvLocalCalibrationCache openCvCache = new();
        private readonly Dictionary<string, CachedCalibrationFile> loadedFiles = new(StringComparer.OrdinalIgnoreCase);
        private IntPtr contextToken;
        private IntPtr lineArityHandle;
        private CachedCalibrationFile? loadedLineArity;
        private LocalCalibrationLayout? loadedLayout;
        private bool disposed;

        public LocalCalibrationCacheManager(string deviceCode)
        {
            this.deviceCode = deviceCode;
            useLegacyCalibration = AppContext.TryGetSwitch("ColorVision.UseLegacyLocalCalibration", out bool enabled) && enabled;
        }

        private SemaphoreSlim NativeGate => useLegacyCalibration ? LegacyNativeGate : openCvGate;

        public string BackendName => useLegacyCalibration ? "cvCamera" : "opencv_helper";

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
                NativeGate.Wait();
                try
                {
                    if (!useLegacyCalibration) return openCvCache.CachedItemCount;
                    return loadedFiles.Count + (loadedLineArity.HasValue ? 1 : 0);
                }
                finally
                {
                    NativeGate.Release();
                }
            }
        }

        public void Execute(
            LocalCalibrationLayout layout,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            IntPtr rawPointer,
            IntPtr ciePointer,
            float[] exposure,
            LocalCalibrationRoi calibrationRoi)
        {
            ArgumentNullException.ThrowIfNull(calibrationFiles);
            ArgumentNullException.ThrowIfNull(exposure);
            if (rawPointer == IntPtr.Zero) throw new ArgumentException("RAW 指针为空。", nameof(rawPointer));

            SemaphoreSlim executionGate = EnterExecution();
            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (!useLegacyCalibration)
                {
                    openCvCache.Execute(layout, calibrationFiles, rawPointer, ciePointer, exposure, calibrationRoi);
                    return;
                }
                CachedCalibrationFile[] files = calibrationFiles.Select(CreateCachedFile).ToArray();
                DeviceCameraCalibrationFile[] colorFiles = calibrationFiles.Where(file => IsColorCalibration(file.CalibrationType)).ToArray();
                if (colorFiles.Length > 1)
                {
                    throw new InvalidOperationException("本地校正一次只能选择一个亮度/颜色校正文件。");
                }
                if (colorFiles.Length == 1 && ciePointer == IntPtr.Zero)
                {
                    throw new ArgumentException("选择亮度/颜色校正后，CIE 输出指针不能为空。", nameof(ciePointer));
                }

                if (loadedLayout.HasValue && loadedLayout.Value != layout)
                {
                    ReleaseNativeContextsCore();
                }
                loadedLayout = layout;

                if (HasChangedCachedFile(files))
                {
                    ReleaseNativeContextsCore();
                    loadedLayout = layout;
                }

                foreach (CachedCalibrationFile file in files)
                {
                    EnsureLoaded(file);
                }

                CachedCalibrationFile[] normalFiles = files.Where(file => !IsColorCalibration(file.CalibrationType)).ToArray();
                int lineArityIndex = Array.FindIndex(normalFiles, file => file.CalibrationType == CalibrationType.LineArity);
                if (lineArityIndex < 0)
                {
                    ExecuteRoutine(layout, normalFiles, rawPointer);
                }
                else
                {
                    ExecuteRoutine(layout, normalFiles.Take(lineArityIndex).ToArray(), rawPointer);
                    ExecuteLineArity(layout, rawPointer);
                    ExecuteRoutine(layout, normalFiles.Skip(lineArityIndex + 1).ToArray(), rawPointer);
                }

                if (colorFiles.Length == 1)
                {
                    CachedCalibrationFile colorFile = files.First(file => IsColorCalibration(file.CalibrationType));
                    EnsureV1Context();
                    ClearSelection();
                    Select(colorFile);
                    if (cvCameraCSLib.CM_TransformV1(
                        contextToken,
                        checked((uint)layout.Width),
                        checked((uint)layout.Height),
                        checked((uint)layout.Bpp),
                        checked((uint)layout.Channels),
                        rawPointer,
                        ciePointer,
                        exposure) == 0)
                    {
                        throw new InvalidOperationException($"生成本地 CIE 内存失败：{colorFile.DisplayName}。");
                    }
                }
            }
            finally
            {
                ExitExecution(executionGate);
            }
        }

        public int ReleaseCache()
        {
            NativeGate.Wait();
            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (!useLegacyCalibration) return openCvCache.Release();
                return ReleaseNativeContextsCore();
            }
            finally
            {
                NativeGate.Release();
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
            NativeGate.Wait();
            try
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    if (useLegacyCalibration)
                    {
                        ReleaseNativeContextsCore();
                    }
                    else
                    {
                        openCvCache.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Release local calibration cache failed: {deviceCode}", ex);
                }
            }
            finally
            {
                NativeGate.Release();
            }
        }

        private SemaphoreSlim EnterExecution()
        {
            bool sharedCacheReadLockHeld = false;
            if (!useLegacyCalibration)
            {
                SharedCacheLifecycleGate.EnterReadLock();
                sharedCacheReadLockHeld = true;
            }

            try
            {
                SemaphoreSlim gate = NativeGate;
                gate.Wait();
                return gate;
            }
            catch
            {
                if (sharedCacheReadLockHeld) SharedCacheLifecycleGate.ExitReadLock();
                throw;
            }
        }

        private void ExitExecution(SemaphoreSlim executionGate)
        {
            try
            {
                executionGate.Release();
            }
            finally
            {
                if (!useLegacyCalibration) SharedCacheLifecycleGate.ExitReadLock();
            }
        }

        private void EnsureLoaded(CachedCalibrationFile file)
        {
            if (file.CalibrationType == CalibrationType.LineArity)
            {
                if (loadedLineArity.HasValue) return;
                lineArityHandle = cvCameraCSLib.CreatCalibrationManage();
                if (lineArityHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("创建线性校正缓存失败。");
                }
                if (cvCameraCSLib.CM_SetCalibParam(lineArityHandle, file.CalibrationType, true, file.FullPath) != 1)
                {
                    _ = cvCameraCSLib.ReleaseCalibrationManage(lineArityHandle);
                    lineArityHandle = IntPtr.Zero;
                    throw new InvalidOperationException($"加载校正文件失败：{file.DisplayName}（{file.FullPath}）。");
                }
                loadedLineArity = file;
                return;
            }

            if (loadedFiles.ContainsKey(file.CacheKey)) return;
            EnsureV1Context();
            if (cvCameraCSLib.CM_LoadItemV1(contextToken, file.CalibrationType, file.Title, file.FullPath) == 0)
            {
                throw new InvalidOperationException($"加载校正文件失败：{file.DisplayName}（{file.FullPath}）。");
            }
            loadedFiles.Add(file.CacheKey, file);
        }

        private void ExecuteRoutine(LocalCalibrationLayout layout, IReadOnlyList<CachedCalibrationFile> files, IntPtr rawPointer)
        {
            if (files.Count == 0) return;
            EnsureV1Context();
            ClearSelection();
            foreach (CachedCalibrationFile file in files)
            {
                Select(file);
            }
            if (cvCameraCSLib.CM_RoutineCalibrationV1(
                contextToken,
                checked((uint)layout.Width),
                checked((uint)layout.Height),
                checked((uint)layout.Bpp),
                checked((uint)layout.Channels),
                rawPointer) == 0)
            {
                throw new InvalidOperationException("执行本地基础校正失败。");
            }
        }

        private void ExecuteLineArity(LocalCalibrationLayout layout, IntPtr rawPointer)
        {
            if (!loadedLineArity.HasValue || lineArityHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("线性校正缓存尚未加载。");
            }
            if (!cvCameraCSLib.CM_SCGD_SDP_LineArity(
                lineArityHandle,
                layout.Width,
                layout.Height,
                layout.Bpp,
                checked((uint)layout.Channels),
                rawPointer))
            {
                throw new InvalidOperationException($"执行本地校正失败：{loadedLineArity.Value.DisplayName}。");
            }
        }

        private void Select(CachedCalibrationFile file)
        {
            if (cvCameraCSLib.CM_SelectItemV1(contextToken, file.CalibrationType, file.Title) == 0)
            {
                throw new InvalidOperationException($"选择校正缓存失败：{file.DisplayName}。");
            }
        }

        private void ClearSelection()
        {
            if (cvCameraCSLib.CM_ClearSelectItemV1(contextToken) == 0)
            {
                throw new InvalidOperationException("清空本地校正选择失败。");
            }
        }

        private void EnsureV1Context()
        {
            if (contextToken != IntPtr.Zero) return;
            IntPtr token = Marshal.AllocHGlobal(1);
            if (token == IntPtr.Zero) throw new OutOfMemoryException("创建本地校正缓存标识失败。");
            if (cvCameraCSLib.CM_InitCalibration(token) == 0)
            {
                Marshal.FreeHGlobal(token);
                throw new InvalidOperationException("初始化本地校正缓存失败。");
            }
            contextToken = token;
        }

        private bool HasChangedCachedFile(IReadOnlyList<CachedCalibrationFile> files)
        {
            int requestedNormalCount = 0;
            CachedCalibrationFile? requestedLineArity = null;
            foreach (CachedCalibrationFile file in files)
            {
                if (file.CalibrationType == CalibrationType.LineArity)
                {
                    requestedLineArity = file;
                    continue;
                }

                requestedNormalCount++;
                if (!loadedFiles.TryGetValue(file.CacheKey, out CachedCalibrationFile cached)
                    || cached.Fingerprint != file.Fingerprint)
                {
                    return true;
                }
            }

            if (requestedNormalCount != loadedFiles.Count) return true;
            if (requestedLineArity.HasValue != loadedLineArity.HasValue) return true;
            return requestedLineArity.HasValue
                && (!string.Equals(requestedLineArity.Value.CacheKey, loadedLineArity!.Value.CacheKey, StringComparison.OrdinalIgnoreCase)
                    || requestedLineArity.Value.Fingerprint != loadedLineArity.Value.Fingerprint);
        }

        private int ReleaseNativeContextsCore()
        {
            int releasedItems = 0;
            Exception? releaseError = null;

            if (lineArityHandle != IntPtr.Zero)
            {
                if (cvCameraCSLib.ReleaseCalibrationManage(lineArityHandle))
                {
                    lineArityHandle = IntPtr.Zero;
                    if (loadedLineArity.HasValue) releasedItems++;
                    loadedLineArity = null;
                }
                else
                {
                    releaseError = new InvalidOperationException("释放线性校正缓存失败。");
                }
            }
            else
            {
                loadedLineArity = null;
            }

            if (contextToken != IntPtr.Zero)
            {
                if (cvCameraCSLib.CM_UnInitCalibration(contextToken) != 0)
                {
                    Marshal.FreeHGlobal(contextToken);
                    contextToken = IntPtr.Zero;
                    releasedItems += loadedFiles.Count;
                    loadedFiles.Clear();
                }
                else
                {
                    releaseError ??= new InvalidOperationException("释放本地 V1 校正缓存失败。");
                }
            }
            else
            {
                loadedFiles.Clear();
            }

            if (contextToken == IntPtr.Zero && lineArityHandle == IntPtr.Zero)
            {
                loadedLayout = null;
            }
            if (releaseError != null) throw releaseError;
            return releasedItems;
        }

        private static CachedCalibrationFile CreateCachedFile(DeviceCameraCalibrationFile file)
        {
            if (!IsSupported(file.CalibrationType))
            {
                throw new NotSupportedException($"本地指针校正暂不支持校正项：{file.DisplayName}（{file.CalibrationType}）。");
            }

            FileInfo fileInfo = new(Path.GetFullPath(file.FullPath));
            fileInfo.Refresh();
            if (!fileInfo.Exists) throw new FileNotFoundException($"校正文件不存在：{file.DisplayName}。", fileInfo.FullName);
            string cacheKey = $"{(int)file.CalibrationType}|{fileInfo.FullName}";
            return new CachedCalibrationFile(
                file.CalibrationType,
                file.DisplayName,
                fileInfo.FullName,
                cacheKey,
                cacheKey,
                new CalibrationFileFingerprint(fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks));
        }

        private static bool IsColorCalibration(CalibrationType type)
            => type is CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor;

        private static bool IsSupported(CalibrationType type)
            => type is CalibrationType.DarkNoise
                or CalibrationType.DefectWPoint
                or CalibrationType.DefectBPoint
                or CalibrationType.DefectPoint
                or CalibrationType.DSNU
                or CalibrationType.Uniformity
                or CalibrationType.Distortion
                or CalibrationType.ColorShift
                or CalibrationType.LineArity
                or CalibrationType.ColorDiff
                or CalibrationType.AngleShift
                or CalibrationType.Luminance
                or CalibrationType.LumOneColor
                or CalibrationType.LumFourColor
                or CalibrationType.LumMultiColor;

        private readonly record struct CachedCalibrationFile(
            CalibrationType CalibrationType,
            string DisplayName,
            string FullPath,
            string CacheKey,
            string Title,
            CalibrationFileFingerprint Fingerprint);

        private readonly record struct CalibrationFileFingerprint(long Length, long LastWriteTimeUtcTicks);
    }

    internal readonly record struct LocalCalibrationLayout(int Width, int Height, int Bpp, int Channels);
}
