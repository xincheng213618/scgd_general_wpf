using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using cvColorVision;
using FlowEngineLib.Algorithm;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using ColorVision.ImageEditor.Tif;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Owns a bounded LRU of opencv_helper calibration contexts. Parsed tables
    /// and geometric maps remain alive while their ordered file revisions
    /// stay cached. LocalCalibrationCacheManager validates inputs and provides
    /// synchronization.
    /// </summary>
    internal sealed class OpenCvLocalCalibrationCache : IDisposable
    {
        // Keep the camera and downstream templates hot without allowing native
        // correction maps to grow with every workflow variant.
        private const int MaxCachedContexts = 2;
        private const int CalibrationHashBufferSize = 256 * 1024;
        private readonly LinkedList<CachedContext> contexts = new();
        private bool disposed;

        public int CachedItemCount => contexts.Sum(entry => entry.Files.Length);

        internal (LocalCalibrationAsset[] Assets, uint InputDepth) Prepare(IReadOnlyList<DeviceCameraCalibrationFile> files)
        {
            if (files.Count == 0) return ([], 0);
            var assets = InExecutionOrder(files).Select(CreateCachedFile).ToArray();
            var context = GetContext(assets);
            return (context.Files, context.InputDepth);
        }

        internal static IEnumerable<DeviceCameraCalibrationFile> InExecutionOrder(IReadOnlyList<DeviceCameraCalibrationFile> files)
        {
            var ordered = files.OrderBy(file => LocalFrameCalibrationService.IsColorCalibration(file.CalibrationType)).ToArray();
            int lastOffset = Array.FindLastIndex(ordered, file => file.CalibrationType == CalibrationType.DSNU);
            if (lastOffset <= 0) return ordered;

            // Correct the dark offset before multiplying the signal. The stable sort moves only
            // DarkNoise stages that precede a DSNU map. Other RAW stages retain their relative order.
            return ordered.Take(lastOffset + 1).OrderBy(file => file.CalibrationType == CalibrationType.DarkNoise)
                .Concat(ordered.Skip(lastOffset + 1));
        }

        public LocalCalibrationExecution Execute(
            LocalCalibrationLayout layout,
            LocalCalibrationAsset[] calibrationFiles,
            IntPtr rawPointer,
            ulong rawLength,
            IntPtr ciePointer,
            ulong cieLength,
            float[] exposure,
            LocalCalibrationRoi calibrationRoi,
            CalibrationOutputMode outputMode,
            CVImageFlipMode rawOutputFlip)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            LocalFrameMirrorService.ValidateFlipMode(rawOutputFlip);
            if (calibrationFiles.Length == 0)
            {
                // RAW-only on a color-only template has no pixel stages, just the requested orientation.
                if (outputMode != CalibrationOutputMode.RawOnly || ciePointer != IntPtr.Zero || cieLength != 0)
                    throw new InvalidOperationException("缺少亮度/颜色模型，无法生成色度输出。");
                if (rawOutputFlip != CVImageFlipMode.None)
                    LocalFrameMirrorService.FlipRaw(rawPointer, checked((int)rawLength), layout.Width, layout.Height,
                        layout.Bpp, layout.Channels, rawOutputFlip);
                return new(null, []);
            }
            CachedContext cachedContext = FlowNodeTiming.Run("LoadCalibrationResources", () => GetContext(calibrationFiles));
            CalibrationFrameRequestV2 request = CreateFrameRequest(layout, exposure, calibrationRoi, outputMode, rawOutputFlip);
            ImageCalibrationSource[] sources = GetSources(cachedContext.Files);
            using var computeStage = FlowNodeTiming.Measure("CalibrationAlgorithm");
            int result = OpenCVMediaHelper.ExecuteCalibrationFrame(cachedContext.Context, in request, rawPointer, rawLength,
                ciePointer, cieLength, out RawColorTransformV1 transform);
            if (result != OpenCVCalibration.CalibrationOk)
            {
                throw CreateNativeException("执行本地校正失败", result, cachedContext.Context);
            }
            computeStage?.Complete();
            return new(outputMode == CalibrationOutputMode.RawOnly ? null : transform, sources);
        }

        internal static CalibrationFrameRequestV2 CreateFrameRequest(
            LocalCalibrationLayout layout, ReadOnlySpan<float> exposure, LocalCalibrationRoi calibrationRoi,
            CalibrationOutputMode outputMode, CVImageFlipMode rawOutputFlip)
        {
            CalibrationFrameRequestV2 request = new()
            {
                Width = checked((uint)layout.Width),
                Height = checked((uint)layout.Height),
                BitsPerChannel = checked((uint)layout.Bpp),
                RawLayout = layout.Channels switch
                {
                    1 => CalibrationRawLayout.Mono,
                    3 => CalibrationRawLayout.BgrInterleaved,
                    _ => throw new ArgumentOutOfRangeException(nameof(layout)),
                },
                OutputMode = outputMode,
                RawFlip = rawOutputFlip switch
                {
                    CVImageFlipMode.None => CalibrationRawFlip.None,
                    CVImageFlipMode.X => CalibrationRawFlip.Vertical,
                    CVImageFlipMode.Y => CalibrationRawFlip.Horizontal,
                    CVImageFlipMode.XY => CalibrationRawFlip.Both,
                    _ => throw new ArgumentOutOfRangeException(nameof(rawOutputFlip)),
                },
            };
            if (!exposure.IsEmpty)
            {
                request.ExposureX = exposure[0];
                request.ExposureY = exposure.Length > 1 ? exposure[1] : request.ExposureX;
                request.ExposureZ = exposure.Length > 2 ? exposure[2] : request.ExposureY;
            }
            if (calibrationRoi.IsConfigured)
            {
                request.RoiX = checked((uint)calibrationRoi.X);
                request.RoiY = checked((uint)calibrationRoi.Y);
                request.RoiWidth = checked((uint)calibrationRoi.Width);
                request.RoiHeight = checked((uint)calibrationRoi.Height);
            }
            return request;
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

        private CachedContext GetContext(LocalCalibrationAsset[] requestedFiles)
        {
            LinkedListNode<CachedContext>? mostRecent = contexts.First;
            CachedContext? previous = mostRecent?.Value;
            LinkedListNode<CachedContext>? cachedNode = Find(contexts, requestedFiles);
            if (cachedNode != null)
            {
                if (cachedNode != mostRecent)
                {
                    TransferRawWorkBuffers(previous, cachedNode.Value);
                    contexts.Remove(cachedNode);
                    contexts.AddFirst(cachedNode);
                }
                return cachedNode.Value;
            }

            // Retain working contexts if the requested revision can no longer be loaded.
            CachedContext created = CreateContext(requestedFiles);
            contexts.AddFirst(created);
            TransferRawWorkBuffers(previous, created);
            if (contexts.Count > MaxCachedContexts) EvictLeastRecentlyUsed(contexts);
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

        private static LinkedListNode<CachedContext>? Find(
            LinkedList<CachedContext> cache,
            IReadOnlyList<LocalCalibrationAsset> requestedFiles)
        {
            LinkedListNode<CachedContext>? node = cache.First;
            while (node != null)
            {
                if (Matches(node.Value, requestedFiles)) return node;
                node = node.Next;
            }
            return null;
        }

        private static bool Matches(
            CachedContext cachedContext,
            IReadOnlyList<LocalCalibrationAsset> requestedFiles)
        {
            if (cachedContext.Context == IntPtr.Zero
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

        private static bool SameFile(LocalCalibrationAsset cached, LocalCalibrationAsset requested)
            => cached.CalibrationType == requested.CalibrationType
                && cached.DisplayName == requested.DisplayName
                && string.Equals(cached.FullPath, requested.FullPath, StringComparison.OrdinalIgnoreCase)
                && cached.Length == requested.Length
                && cached.LastWriteTimeUtcTicks == requested.LastWriteTimeUtcTicks
                && cached.Revision == requested.Revision
                && (requested.Sha256 == null || requested.Sha256 == cached.Sha256);

        private static CachedContext CreateContext(LocalCalibrationAsset[] requestedFiles)
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
                LocalCalibrationAsset[] loaded = new LocalCalibrationAsset[requestedFiles.Length];
                for (int index = 0; index < requestedFiles.Length; index++)
                {
                    LocalCalibrationAsset file = requestedFiles[index];
                    // Keep the file stable while fingerprinting and loading the native resource.
                    using FileStream source = new(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (source.Length != file.Length || File.GetLastWriteTimeUtc(file.FullPath).Ticks != file.LastWriteTimeUtcTicks
                        || LocalCalibrationFileRevision.Read(source.SafeFileHandle) != file.Revision)
                        throw new IOException($"校正资源“{file.DisplayName}”已变化，请重新准备模板。");
                    int loadResult;
                    string? digest = null;
                    if ((file.CalibrationType is CalibrationType.DSNU or CalibrationType.Uniformity)
                        && OpenCVCalibration.TryLoadCalibrationMapWithSha256(
                            newContext, (int)file.CalibrationType, file.FullPath, nativeSha256, out loadResult))
                    {
                        if (loadResult == OpenCVCalibration.CalibrationOk)
                            digest = Convert.ToHexString(nativeSha256);
                    }
                    else
                    {
                        digest = ComputeSourceHash(source, file.Length);
                        loadResult = OpenCVCalibration.M_CalibrationLoadFileW(newContext, (int)file.CalibrationType, file.FullPath);
                    }

                    if (loadResult != OpenCVCalibration.CalibrationOk)
                    {
                        throw CreateNativeException(
                            $"加载校正文件失败：{file.DisplayName}（{file.FullPath}）",
                            loadResult,
                            newContext);
                    }
                    if (file.Sha256 != null && file.Sha256 != digest)
                        throw new IOException($"校正资源“{file.DisplayName}”的内容已变化，请重新准备模板。");
                    loaded[index] = file with { Sha256 = digest };
                }

                uint inputDepth;
                try
                {
                    int depthResult = OpenCVMediaHelper.GetCalibrationInputDepth(newContext, out inputDepth);
                    if (depthResult != OpenCVCalibration.CalibrationOk)
                        throw CreateNativeException("校正模板的 RAW 位深要求不兼容", depthResult, newContext);
                }
                catch (EntryPointNotFoundException error)
                {
                    throw new InvalidOperationException("请更新 opencv_helper.dll，当前版本缺少模板位深接口 M_CalibrationGetInputDepthV1。", error);
                }
                return new CachedContext(newContext, loaded, inputDepth);
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

        private static LocalCalibrationAsset CreateCachedFile(DeviceCameraCalibrationFile file)
        {
            if (!IsSupported(file.CalibrationType))
            {
                throw new NotSupportedException($"本地指针校正暂不支持校正项：{file.DisplayName}（{file.CalibrationType}）。");
            }

            string path = Path.GetFullPath(file.FullPath);
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var revision = LocalCalibrationFileRevision.Read(source.SafeFileHandle);
            return new LocalCalibrationAsset(
                file.CalibrationType,
                file.DisplayName,
                path,
                source.Length,
                File.GetLastWriteTimeUtc(path).Ticks,
                revision,
                revision == null ? ComputeSourceHash(source, source.Length) : null);
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

        private static ImageCalibrationSource[] GetSources(LocalCalibrationAsset[] files)
            => files.Select(file => new ImageCalibrationSource
            {
                CalibrationType = (int)file.CalibrationType, Name = file.DisplayName,
                FileName = Path.GetFileName(file.FullPath), FileBytes = file.Length,
                FileModifiedUtc = new DateTime(file.LastWriteTimeUtcTicks, DateTimeKind.Utc), FileSha256 = file.Sha256
            }).ToArray();

        private sealed class CachedContext(IntPtr context, LocalCalibrationAsset[] files, uint inputDepth)
        {
            public IntPtr Context { get; set; } = context;
            public LocalCalibrationAsset[] Files { get; } = files;
            public uint InputDepth { get; } = inputDepth;
            public bool UsesRawWorkBuffers { get; } = files.Any(file => file.CalibrationType is
                CalibrationType.Distortion or CalibrationType.ColorShift or CalibrationType.ColorDiff or CalibrationType.AngleShift);
        }
    }

    internal sealed record LocalCalibrationAsset(CalibrationType CalibrationType, string DisplayName, string FullPath,
        long Length, long LastWriteTimeUtcTicks, LocalCalibrationFileRevision? Revision, string? Sha256 = null);

    // A replaced or rewritten file can retain its length and last-write timestamp. Keep the
    // same revision identity as the native asset cache; hash directly on volumes lacking it.
    internal readonly record struct LocalCalibrationFileRevision(uint Volume, ulong FileId, long ChangeTime)
    {
        internal static LocalCalibrationFileRevision? Read(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation file)
                || !GetFileInformationByHandleEx(handle, 0 /* FileBasicInfo */, out FileBasicInformation basic,
                    (uint)Marshal.SizeOf<FileBasicInformation>())) return null;
            ulong id = ((ulong)file.FileIndexHigh << 32) | file.FileIndexLow;
            return id != 0 && basic.ChangeTime != 0 ? new(file.VolumeSerialNumber, id, basic.ChangeTime) : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
            public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileBasicInformation
        {
            public long CreationTime, LastAccessTime, LastWriteTime, ChangeTime;
            public uint FileAttributes;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation info);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass,
            out FileBasicInformation info, uint size);
    }
}
