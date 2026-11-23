using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using cvColorVision;
using FlowEngineLib.Algorithm;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ColorVision.ImageEditor.Tif;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Owns a bounded LRU of opencv_helper calibration contexts. Parsed tables
    /// and geometric maps remain alive while their layout and ordered file set
    /// stay cached. LocalCalibrationCacheManager validates inputs and provides
    /// synchronization.
    /// </summary>
    internal sealed class OpenCvLocalCalibrationCache : IDisposable
    {
        // Keep the camera and downstream templates hot without allowing native
        // correction maps to grow with every workflow variant.
        private const int MaxCachedContexts = 2;
        // Deferred color contexts hold only calibration parameters. These two
        // slots add no RAW scratch or spatial map capacity to the general cache.
        private const int MaxDeferredColorContexts = 2;
        private const int CalibrationHashBufferSize = 256 * 1024;
        private readonly LinkedList<CachedContext> contexts = new();
        private readonly LinkedList<CachedContext> deferredColorContexts = new();
        private bool disposed;

        public int CachedItemCount => contexts.Sum(entry => entry.Files.Length)
            + deferredColorContexts.Sum(entry => entry.Files.Length);

        public LocalCalibrationExecution Execute(
            LocalCalibrationLayout layout,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            IntPtr rawPointer,
            IntPtr ciePointer,
            float[] exposure,
            LocalCalibrationRoi calibrationRoi,
            bool allowAcceleration = false,
            CVImageFlipMode rawOutputFlip = CVImageFlipMode.None)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            LocalFrameMirrorService.ValidateFlipMode(rawOutputFlip);
            bool hasBasicCalibration = calibrationFiles.Any(file => !IsColorCalibration(file.CalibrationType));
            if (rawOutputFlip != CVImageFlipMode.None
                && (!hasBasicCalibration || (!allowAcceleration && calibrationFiles.Any(file => IsColorCalibration(file.CalibrationType)))))
                throw new ArgumentException("合并翻转要求校正包含常规步骤且输出 RAW。", nameof(rawOutputFlip));

            if (allowAcceleration && calibrationFiles.Any(file => IsColorCalibration(file.CalibrationType)))
            {
                // Extract the color parameters before executing the basic stages, without allocating XYZ.
                DeviceCameraCalibrationFile[] colorFiles = calibrationFiles.Where(file => IsColorCalibration(file.CalibrationType)).ToArray();
                CachedContext colorContext = FlowNodeTiming.Run("LoadCalibrationResources",
                    () => Prepare(layout, colorFiles, ciePointer, deferredColorOnly: true));
                CalibrationExecutionOptionsV1 deferredOptions = CreateExecutionOptions(exposure, calibrationRoi);
                RawColorTransformV1 deferred = RawColorTransformV1.Create();
                int deferredResult = OpenCVMediaHelper.M_CalibrationGetColorTransformV1(colorContext.Context, in deferredOptions, ref deferred);
                if (deferredResult != OpenCVCalibration.CalibrationOk)
                    throw CreateNativeException("读取色度校正参数失败", deferredResult, colorContext.Context);
                DeviceCameraCalibrationFile[] basicFiles = calibrationFiles.Where(file => !IsColorCalibration(file.CalibrationType)).ToArray();
                ImageCalibrationSource[] basicSources = [];
                if (basicFiles.Length > 0)
                    basicSources = Execute(layout, basicFiles, rawPointer, IntPtr.Zero, exposure, calibrationRoi, rawOutputFlip: rawOutputFlip).Sources;
                else FlowNodeTiming.Skip("CalibrationAlgorithm");
                return new(deferred, basicSources.Concat(GetSources(colorContext.Files)).ToArray());
            }

            CachedContext cachedContext = FlowNodeTiming.Run("LoadCalibrationResources", () => Prepare(layout, calibrationFiles, ciePointer));
            CalibrationExecutionOptionsV1 options = CreateExecutionOptions(exposure, calibrationRoi);
            (ulong rawByteLength, ulong cieFloatCount) = GetBufferLengths(layout, cachedContext.Files);
            using var computeStage = FlowNodeTiming.Measure("CalibrationAlgorithm");
            int result = rawOutputFlip != CVImageFlipMode.None
                ? OpenCVCalibration.M_CalibrationExecuteRawWithFlipV1(
                    cachedContext.Context, checked((uint)layout.Width), checked((uint)layout.Height),
                    checked((uint)layout.Bpp), checked((uint)layout.Channels), rawPointer, rawByteLength,
                    in options, (int)rawOutputFlip)
                : OpenCVCalibration.M_CalibrationExecute(
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
            computeStage?.Complete();
            if (!cachedContext.Files.Any(file => IsColorCalibration(file.CalibrationType))) return new(null, GetSources(cachedContext.Files));
            RawColorTransformV1 transform = RawColorTransformV1.Create();
            int snapshotResult = OpenCVMediaHelper.M_CalibrationGetColorTransformV1(cachedContext.Context, in options, ref transform);
            if (snapshotResult != OpenCVCalibration.CalibrationOk)
                throw CreateNativeException("读取已执行的色度校正参数失败", snapshotResult, cachedContext.Context);
            return new(transform, GetSources(cachedContext.Files));
        }

        internal static CalibrationExecutionOptionsV1 CreateExecutionOptions(float[] exposure, LocalCalibrationRoi calibrationRoi)
        {
            CalibrationExecutionOptionsV1 options = CalibrationExecutionOptionsV1.Create(exposure);
            if (!calibrationRoi.IsConfigured)
            {
                return options;
            }

            options.RoiX = checked((uint)calibrationRoi.X);
            options.RoiY = checked((uint)calibrationRoi.Y);
            options.RoiWidth = checked((uint)calibrationRoi.Width);
            options.RoiHeight = checked((uint)calibrationRoi.Height);
            return options;
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
            IntPtr ciePointer,
            bool deferredColorOnly = false)
        {
            CachedCalibrationFile[] requestedFiles = calibrationFiles.Select(CreateCachedFile).ToArray();
            int colorFileCount = requestedFiles.Count(file => IsColorCalibration(file.CalibrationType));
            if (colorFileCount > 1) throw new InvalidOperationException("本地校正一次只能选择一个亮度/颜色校正文件。");
            if (colorFileCount == 1 && ciePointer == IntPtr.Zero && !deferredColorOnly)
            {
                throw new ArgumentException("选择亮度/颜色校正后，CIE 输出指针不能为空。", nameof(ciePointer));
            }

            LinkedList<CachedContext> cache = deferredColorOnly ? deferredColorContexts : contexts;
            LinkedListNode<CachedContext>? mostRecent = cache.First;
            CachedContext? previous = deferredColorOnly ? null : mostRecent?.Value;
            LinkedListNode<CachedContext>? cachedNode = Find(cache, layout, requestedFiles);
            if (cachedNode != null)
            {
                if (cachedNode != mostRecent)
                {
                    TransferRawWorkBuffers(previous, cachedNode.Value);
                    cache.Remove(cachedNode);
                    cache.AddFirst(cachedNode);
                }
                return cachedNode.Value;
            }

            int capacity = deferredColorOnly ? MaxDeferredColorContexts : MaxCachedContexts;
            if (cache.Count >= capacity)
            {
                EvictLeastRecentlyUsed(cache);
            }

            // Keep the donor's buffers until the requested file set has loaded successfully.
            CachedContext created = CreateContext(layout, requestedFiles);
            cache.AddFirst(created);
            TransferRawWorkBuffers(previous, created);
            return created;
        }

        private static void TransferRawWorkBuffers(CachedContext? previous, CachedContext requested)
        {
            if (previous?.UsesRawWorkBuffers == true && requested.UsesRawWorkBuffers)
            {
                // An older DLL or a rejected transfer leaves both contexts usable independently.
                _ = OpenCVCalibration.TryTransferCalibrationWorkBuffers(requested.Context, previous.Context);
            }
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

        private static LinkedListNode<CachedContext>? Find(
            LinkedList<CachedContext> cache,
            LocalCalibrationLayout layout,
            IReadOnlyList<CachedCalibrationFile> requestedFiles)
        {
            LinkedListNode<CachedContext>? node = cache.First;
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
                Span<byte> nativeSha256 = stackalloc byte[SHA256.HashSizeInBytes];
                foreach (CachedCalibrationFile file in requestedFiles)
                {
                    // Keep the file stable while fingerprinting and loading the native resource.
                    using FileStream source = new(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (source.Length != file.Length || File.GetLastWriteTimeUtc(file.FullPath).Ticks != file.LastWriteTimeUtcTicks)
                        throw new IOException("校正资源在加载前已变化，请重试。");
                    int loadResult;
                    if ((file.CalibrationType is CalibrationType.DSNU or CalibrationType.Uniformity)
                        && OpenCVCalibration.TryLoadCalibrationMapWithSha256(
                            newContext, (int)file.CalibrationType, file.FullPath, nativeSha256, out loadResult))
                    {
                        if (loadResult == OpenCVCalibration.CalibrationOk)
                            file.Sha256 = Convert.ToHexString(nativeSha256);
                    }
                    else
                    {
                        file.Sha256 = ComputeSourceHash(source, file.Length);
                        loadResult = OpenCVCalibration.M_CalibrationLoadFileW(newContext, (int)file.CalibrationType, file.FullPath);
                    }

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

        private static string ComputeSourceHash(FileStream source, long length)
        {
            if (length < CalibrationHashBufferSize)
                return Convert.ToHexString(SHA256.HashData(source));

            // Large tables need fewer file reads and hash updates with a bounded buffer.
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(CalibrationHashBufferSize);
            try
            {
                int read;
                while ((read = source.Read(buffer.AsSpan(0, CalibrationHashBufferSize))) > 0)
                    hash.AppendData(buffer.AsSpan(0, read));

                return Convert.ToHexString(hash.GetHashAndReset());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        private static void EvictLeastRecentlyUsed(LinkedList<CachedContext> cache)
        {
            LinkedListNode<CachedContext>? leastRecentlyUsed = cache.Last;
            if (leastRecentlyUsed == null) return;

            cache.Remove(leastRecentlyUsed);
            IntPtr contextToRelease = leastRecentlyUsed.Value.Context;
            leastRecentlyUsed.Value.Context = IntPtr.Zero;
            Exception? releaseError = TryDestroyContext(contextToRelease);
            if (releaseError != null) throw releaseError;
        }

        private int ReleaseCore()
        {
            int released = CachedItemCount;
            List<Exception>? releaseErrors = null;
            foreach (LinkedList<CachedContext> cache in new[] { contexts, deferredColorContexts })
            {
                while (cache.First != null)
                {
                    CachedContext cachedContext = cache.First.Value;
                    cache.RemoveFirst();
                    IntPtr contextToRelease = cachedContext.Context;
                    cachedContext.Context = IntPtr.Zero;
                    Exception? releaseError = TryDestroyContext(contextToRelease);
                    if (releaseError != null)
                    {
                        (releaseErrors ??= new List<Exception>()).Add(releaseError);
                    }
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

        private static ImageCalibrationSource[] GetSources(IEnumerable<CachedCalibrationFile> files)
            => files.Select(file => new ImageCalibrationSource
            {
                CalibrationType = (int)file.CalibrationType, Name = file.DisplayName,
                FileName = Path.GetFileName(file.FullPath), FileBytes = file.Length,
                FileModifiedUtc = new DateTime(file.LastWriteTimeUtcTicks, DateTimeKind.Utc), FileSha256 = file.Sha256
            }).ToArray();

        private sealed record CachedCalibrationFile(
            CalibrationType CalibrationType,
            string DisplayName,
            string FullPath,
            long Length,
            long LastWriteTimeUtcTicks)
        {
            public string? Sha256 { get; set; }
        }

        private sealed class CachedContext(
            IntPtr context,
            LocalCalibrationLayout layout,
            CachedCalibrationFile[] files)
        {
            public IntPtr Context { get; set; } = context;
            public LocalCalibrationLayout Layout { get; } = layout;
            public CachedCalibrationFile[] Files { get; } = files;
            public bool UsesRawWorkBuffers { get; } = files.Any(file => file.CalibrationType is
                CalibrationType.Distortion or CalibrationType.ColorShift or CalibrationType.ColorDiff or CalibrationType.AngleShift);
        }
    }
}
