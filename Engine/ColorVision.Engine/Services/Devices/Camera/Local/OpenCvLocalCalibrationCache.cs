using ColorVision.Core;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Owns a bounded LRU of opencv_helper calibration contexts. Parsed tables
    /// and geometric maps remain alive while their layout and ordered file set
    /// stay cached. Callers provide synchronization.
    /// </summary>
    internal sealed class OpenCvLocalCalibrationCache : IDisposable
    {
        // Keep the camera and downstream templates hot without allowing native
        // correction maps to grow with every workflow variant.
        private const int MaxCachedContexts = 2;
        private readonly LinkedList<CachedContext> contexts = new();
        private bool disposed;

        public int CachedItemCount => contexts.Sum(entry => entry.Files.Length);

        public void Execute(
            LocalCalibrationLayout layout,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            IntPtr rawPointer,
            IntPtr ciePointer,
            float[] exposure)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(calibrationFiles);
            ArgumentNullException.ThrowIfNull(exposure);
            if (rawPointer == IntPtr.Zero) throw new ArgumentException("RAW pointer is null.", nameof(rawPointer));

            CachedContext cachedContext = Prepare(layout, calibrationFiles, ciePointer);
            CalibrationExecutionOptionsV1 options = CalibrationExecutionOptionsV1.Create(exposure);
            (ulong rawByteLength, ulong cieFloatCount) = GetBufferLengths(layout, cachedContext.Files);
            int result = OpenCVCalibration.M_CalibrationExecute(
                cachedContext.Context,
                checked((uint)layout.Width),
                checked((uint)layout.Height),
                checked((uint)layout.Bpp),
                checked((uint)layout.Channels),
                rawPointer,
                rawByteLength,
                ciePointer,
                cieFloatCount,
                in options);
            if (result != OpenCVCalibration.CalibrationOk)
            {
                throw CreateNativeException("执行本地校正失败", result, cachedContext.Context);
            }
        }

        public int Release()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return ReleaseCore();
        }

        public void Dispose()
        {
            if (disposed) return;
            try
            {
                _ = ReleaseCore();
            }
            finally
            {
                disposed = true;
            }
        }

        private CachedContext Prepare(
            LocalCalibrationLayout layout,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            IntPtr ciePointer)
        {
            CachedCalibrationFile[] requestedFiles = calibrationFiles.Select(CreateCachedFile).ToArray();
            int colorFileCount = requestedFiles.Count(file => IsColorCalibration(file.CalibrationType));
            if (colorFileCount > 1) throw new InvalidOperationException("本地校正一次只能选择一个亮度/颜色校正文件。");
            if (colorFileCount == 1 && ciePointer == IntPtr.Zero)
            {
                throw new ArgumentException("选择亮度/颜色校正后，CIE 输出指针不能为空。", nameof(ciePointer));
            }

            LinkedListNode<CachedContext>? cachedNode = Find(layout, requestedFiles);
            if (cachedNode != null)
            {
                contexts.Remove(cachedNode);
                contexts.AddFirst(cachedNode);
                return cachedNode.Value;
            }

            if (contexts.Count >= MaxCachedContexts)
            {
                EvictLeastRecentlyUsed();
            }

            CachedContext created = CreateContext(layout, requestedFiles);
            contexts.AddFirst(created);
            return created;
        }

        private static (ulong RawByteLength, ulong CieFloatCount) GetBufferLengths(
            LocalCalibrationLayout layout,
            IReadOnlyList<CachedCalibrationFile> files)
        {
            ulong pixelCount = checked((ulong)layout.Width * (ulong)layout.Height);
            ulong rawByteLength = checked(pixelCount * (ulong)layout.Channels * (ulong)(layout.Bpp / 8));
            ulong cieFloatCount = files.Any(file => file.CalibrationType == CalibrationType.Luminance)
                ? pixelCount
                : files.Any(file => IsColorCalibration(file.CalibrationType))
                    ? checked(pixelCount * 3)
                    : 0;
            return (rawByteLength, cieFloatCount);
        }

        private LinkedListNode<CachedContext>? Find(
            LocalCalibrationLayout layout,
            IReadOnlyList<CachedCalibrationFile> requestedFiles)
        {
            LinkedListNode<CachedContext>? node = contexts.First;
            while (node != null)
            {
                if (Matches(node.Value, layout, requestedFiles)) return node;
                node = node.Next;
            }
            return null;
        }

        private static bool Matches(
            CachedContext cachedContext,
            LocalCalibrationLayout layout,
            IReadOnlyList<CachedCalibrationFile> requestedFiles)
        {
            if (cachedContext.Context == IntPtr.Zero
                || cachedContext.Layout != layout
                || cachedContext.Files.Length != requestedFiles.Count)
            {
                return false;
            }

            for (int index = 0; index < cachedContext.Files.Length; index++)
            {
                if (!SameFile(cachedContext.Files[index], requestedFiles[index])) return false;
            }
            return true;
        }

        private static bool SameFile(CachedCalibrationFile cached, CachedCalibrationFile requested)
            => cached.CalibrationType == requested.CalibrationType
                && string.Equals(cached.FullPath, requested.FullPath, StringComparison.OrdinalIgnoreCase)
                && cached.Length == requested.Length
                && cached.LastWriteTimeUtcTicks == requested.LastWriteTimeUtcTicks;

        private static CachedContext CreateContext(LocalCalibrationLayout layout, CachedCalibrationFile[] requestedFiles)
        {
            IntPtr newContext = IntPtr.Zero;
            int createResult = OpenCVCalibration.M_CalibrationCreate(out newContext);
            if (createResult != OpenCVCalibration.CalibrationOk || newContext == IntPtr.Zero)
            {
                Exception createError = CreateNativeException("创建本地校正上下文失败", createResult, newContext);
                Exception? cleanupError = TryDestroyContext(newContext);
                throw cleanupError == null ? createError : new AggregateException(createError, cleanupError);
            }

            try
            {
                foreach (CachedCalibrationFile file in requestedFiles)
                {
                    int loadResult = OpenCVCalibration.M_CalibrationLoadFileW(newContext, (int)file.CalibrationType, file.FullPath);
                    if (loadResult != OpenCVCalibration.CalibrationOk)
                    {
                        throw CreateNativeException(
                            $"加载校正文件失败：{file.DisplayName}（{file.FullPath}）",
                            loadResult,
                            newContext);
                    }
                }

                return new CachedContext(newContext, layout, requestedFiles);
            }
            catch (Exception loadError)
            {
                Exception? cleanupError = TryDestroyContext(newContext);
                if (cleanupError != null) throw new AggregateException(loadError, cleanupError);
                throw;
            }
        }

        private void EvictLeastRecentlyUsed()
        {
            LinkedListNode<CachedContext>? leastRecentlyUsed = contexts.Last;
            if (leastRecentlyUsed == null) return;

            contexts.Remove(leastRecentlyUsed);
            IntPtr contextToRelease = leastRecentlyUsed.Value.Context;
            leastRecentlyUsed.Value.Context = IntPtr.Zero;
            Exception? releaseError = TryDestroyContext(contextToRelease);
            if (releaseError != null) throw releaseError;
        }

        private int ReleaseCore()
        {
            int released = CachedItemCount;
            List<Exception>? releaseErrors = null;
            while (contexts.First != null)
            {
                CachedContext cachedContext = contexts.First.Value;
                contexts.RemoveFirst();
                IntPtr contextToRelease = cachedContext.Context;
                cachedContext.Context = IntPtr.Zero;
                Exception? releaseError = TryDestroyContext(contextToRelease);
                if (releaseError != null)
                {
                    (releaseErrors ??= new List<Exception>()).Add(releaseError);
                }
            }

            if (releaseErrors?.Count == 1) throw releaseErrors[0];
            if (releaseErrors?.Count > 1) throw new AggregateException("释放本地校正上下文失败。", releaseErrors);
            return released;
        }

        private static Exception? TryDestroyContext(IntPtr context)
        {
            if (context == IntPtr.Zero) return null;
            try
            {
                int result = OpenCVCalibration.M_CalibrationDestroy(context);
                return result == OpenCVCalibration.CalibrationOk
                    ? null
                    : CreateNativeException("释放本地校正上下文失败", result, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static InvalidOperationException CreateNativeException(string operation, int result, IntPtr errorContext)
        {
            string detail = OpenCVCalibration.GetCalibrationError(errorContext);
            return new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                ? $"{operation}，原生错误码：{result}。"
                : $"{operation}，原生错误码：{result}，{detail}");
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
            return new CachedCalibrationFile(
                file.CalibrationType,
                file.DisplayName,
                fileInfo.FullName,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.Ticks);
        }

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

        private static bool IsColorCalibration(CalibrationType type)
            => type is CalibrationType.Luminance
                or CalibrationType.LumOneColor
                or CalibrationType.LumFourColor
                or CalibrationType.LumMultiColor;

        private readonly record struct CachedCalibrationFile(
            CalibrationType CalibrationType,
            string DisplayName,
            string FullPath,
            long Length,
            long LastWriteTimeUtcTicks);

        private sealed class CachedContext(
            IntPtr context,
            LocalCalibrationLayout layout,
            CachedCalibrationFile[] files)
        {
            public IntPtr Context { get; set; } = context;
            public LocalCalibrationLayout Layout { get; } = layout;
            public CachedCalibrationFile[] Files { get; } = files;
        }
    }
}
