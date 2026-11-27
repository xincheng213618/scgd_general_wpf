using FlowEngineLib.Base;
using FlowEngineLib.Algorithm;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using ColorVision.ImageEditor.Tif;
using Microsoft.Win32.SafeHandles;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    public enum LocalFrameBufferKind
    {
        Source,
        CvRaw,
        CvCie
    }

    public sealed class LocalFrameMetadata
    {
        public int Width { get; init; }
        public int Height { get; init; }
        /// <summary>Bits per channel of the current RAW buffer. Acquisition records the camera's original bit depth.</summary>
        public int SourceBpp { get; init; }
        public int CieBpp { get; init; } = 32;
        public int Channels { get; init; }
        public float Gain { get; init; }
        public float[] Exposure { get; init; } = Array.Empty<float>();
        public string DeviceCode { get; init; } = string.Empty;
        public string SourceFilePath { get; init; } = string.Empty;
        // The source file can retain 8-bit samples while the working RAW holds promoted 16-bit samples.
        internal int SourceFileBpp { get; init; }
        public string CalibrationTemplate { get; init; } = string.Empty;
        public DateTime CaptureTime { get; init; } = DateTime.Now;
        public LocalFrameBufferKind PrimaryBufferKind { get; init; }
        public CVImageFlipMode FlipMode { get; init; } = CVImageFlipMode.None;
        /// <summary>Whether all sensor-coordinate transforms have completed.</summary>
        public bool IsMirrorReady { get; init; }
        public ImageAcquisitionRecord? Acquisition { get; init; }
    }

    /// <summary>
    /// Owns the root reference to process-local RAW memory and optional CIE data memory.
    /// Consumers must use <see cref="Acquire"/> and dispose the returned lease.
    /// </summary>
    public sealed class LocalFlowFrame : IDisposable
    {
        private SharedRawStorage storage;
        private CieBuffer? cieBuffer;
        private readonly object flipSync = new();
        private int disposed;

        private LocalFlowFrame(SharedRawStorage storage, LocalFrameMetadata metadata)
        {
            this.storage = storage;
            Metadata = metadata;
            FrameId = Guid.NewGuid();
        }

        public Guid FrameId { get; }
        public LocalFrameMetadata Metadata { get; private set; }
        public int MasterId { get; set; } = -1;
        public string CvRawFilePath { get; set; } = string.Empty;
        internal bool? SavedRawFlipApplied { get; set; }
        public string CvCieFilePath { get; set; } = string.Empty;
        internal ColorCalibrationSnapshot? ColorCalibration { get; set; }
        internal ImageCalibrationSummary? CalibrationProvenance { get; set; }
        public bool HasRaw => storage.RawLength > 0;
        public bool HasCie => Volatile.Read(ref cieBuffer) != null;
        public bool IsRawFlipApplied => Metadata.FlipMode == CVImageFlipMode.None || storage.IsFlipApplied;
        public bool IsCieFlipApplied => Metadata.FlipMode == CVImageFlipMode.None || cieBuffer?.IsFlipApplied == true;
        public bool IsFlipApplied => Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie ? IsCieFlipApplied : IsRawFlipApplied;

        internal void MarkPrimaryBufferFlipApplied()
        {
            if (Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie) cieBuffer?.MarkFlipApplied();
            else storage.MarkFlipApplied();
        }

        public static LocalFlowFrame Allocate(LocalFrameMetadata metadata, int rawLength, int cieLength)
            => Allocate(metadata, rawLength, cieLength, null);

        internal static LocalFlowFrame Allocate(LocalFrameMetadata metadata, int rawLength, int cieLength, LocalCameraRawBufferPool? rawBufferPool)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentOutOfRangeException.ThrowIfNegative(rawLength);
            ArgumentOutOfRangeException.ThrowIfNegative(cieLength);
            if (rawLength == 0 && cieLength == 0) throw new ArgumentException("At least one image buffer is required.");
            return Allocate(metadata, new SharedRawStorage(rawLength, rawBufferPool), cieLength);
        }

        internal static LocalFlowFrame AdoptCameraRaw(LocalFrameMetadata metadata, int rawLength, int cieLength,
            IntPtr pixels, SafeHandle owner)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rawLength);
            ArgumentOutOfRangeException.ThrowIfNegative(cieLength);
            ArgumentNullException.ThrowIfNull(owner);
            if (pixels == IntPtr.Zero || owner.IsClosed || owner.IsInvalid)
                throw new ArgumentException("A live camera RAW buffer and its owner are required.");
            return Allocate(metadata, new SharedRawStorage(rawLength, null, pixels, owner), cieLength);
        }

        private static LocalFlowFrame Allocate(LocalFrameMetadata metadata, SharedRawStorage raw, int cieLength)
        {
            try
            {
                return new LocalFlowFrame(raw, metadata) { cieBuffer = cieLength > 0 ? new CieBuffer(cieLength) : null };
            }
            catch
            {
                raw.ReleaseReference();
                throw;
            }
        }

        internal void PrepareForCalibration(string calibrationTemplate, int cieLength, bool hasBasicCalibration, float[]? exposure = null)
        {
            lock (flipSync)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                ArgumentOutOfRangeException.ThrowIfNegative(cieLength);
                LocalFrameMetadata metadata = CreateCalibrationMetadata(calibrationTemplate, cieLength, exposure);
                if ((cieBuffer?.Length ?? 0) != cieLength)
                    ReplaceCieBuffer(cieLength > 0 ? new CieBuffer(cieLength) : null);
                CommitCalibration(metadata, hasBasicCalibration, default);
            }
        }

        internal void ApplyCalibration(string calibrationTemplate, int cieLength, bool hasBasicCalibration,
            float[]? exposure, Func<LocalFlowFrameLease, IntPtr, CalibrationUpdate> apply, bool promoteRaw8To16 = false)
        {
            lock (flipSync)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                bool promote = promoteRaw8To16 && Metadata.SourceBpp == 8;
                LocalFrameMetadata metadata = CreateCalibrationMetadata(calibrationTemplate, cieLength, exposure,
                    workingBpp: promote ? 16 : Metadata.SourceBpp);
                bool writesRaw = hasBasicCalibration || (cieLength == 0 && !IsRawFlipApplied);
                bool restoreSensorOrder = hasBasicCalibration && Metadata.FlipMode != CVImageFlipMode.None && storage.IsFlipApplied;
                SharedRawStorage? writableRaw = null;
                CieBuffer? output = null;
                try
                {
                    if (promote)
                        writableRaw = storage.PromoteRaw8To16(Metadata);
                    else if (restoreSensorOrder || (writesRaw && HasRaw && storage.HasOtherOwners))
                        writableRaw = storage.Copy();
                    if (restoreSensorOrder)
                        writableRaw!.RestoreSensorOrder(metadata);
                    using LocalFlowFrameLease source = AcquireLease(writableRaw ?? storage, promote ? metadata : Metadata);
                    output = cieLength > 0 ? new CieBuffer(cieLength) : null;
                    CalibrationUpdate update = apply(source, output?.DangerousGetHandle() ?? IntPtr.Zero);
                    if (writableRaw != null)
                    {
                        SharedRawStorage previous = storage;
                        storage = writableRaw;
                        writableRaw = null;
                        previous.ReleaseReference();
                    }
                    if (update.RawFlipApplied) storage.MarkFlipApplied();
                    output?.MarkFlipApplied();
                    ReplaceCieBuffer(output);
                    output = null;
                    CommitCalibration(metadata, hasBasicCalibration || promote, update);
                }
                finally
                {
                    output?.Dispose();
                    writableRaw?.ReleaseReference();
                }
            }
        }

        private void ReplaceCieBuffer(CieBuffer? replacement)
            => Interlocked.Exchange(ref cieBuffer, replacement)?.Dispose();

        internal LocalFlowFrame ForkForCalibration()
        {
            lock (flipSync)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                if (!HasRaw || storage.IsFlipFailed)
                    throw new InvalidOperationException("当前帧没有可用的 RAW 内存。");
                LocalFrameMetadata metadata = CreateCalibrationMetadata(Metadata.CalibrationTemplate, 0, null,
                    Metadata.IsMirrorReady, string.IsNullOrWhiteSpace(CvRawFilePath) ? null : CvRawFilePath);
                LocalFlowFrame output = new(storage, metadata)
                {
                    ColorCalibration = ColorCalibration,
                    CalibrationProvenance = CalibrationProvenance,
                    CvRawFilePath = CvRawFilePath,
                    SavedRawFlipApplied = SavedRawFlipApplied,
                    MasterId = MasterId,
                };
                storage.AddReference();
                return output;
            }
        }

        internal readonly record struct CalibrationUpdate(ColorCalibrationSnapshot? Snapshot,
            ImageCalibrationSummary? Provenance, bool RawFlipApplied);

        private LocalFrameMetadata CreateCalibrationMetadata(string template, int cieLength, float[]? exposure,
            bool isMirrorReady = true, string? inputPath = null, int? workingBpp = null)
        {
            LocalFrameMetadata source = Metadata;
            return new LocalFrameMetadata
            {
                Width = source.Width,
                Height = source.Height,
                SourceBpp = workingBpp ?? source.SourceBpp,
                CieBpp = source.CieBpp,
                Channels = source.Channels,
                Gain = source.Gain,
                Exposure = exposure ?? source.Exposure,
                DeviceCode = source.DeviceCode,
                SourceFilePath = inputPath ?? source.SourceFilePath,
                SourceFileBpp = inputPath != null || source.SourceFileBpp == 0 ? source.SourceBpp : source.SourceFileBpp,
                CalibrationTemplate = template,
                CaptureTime = source.CaptureTime,
                Acquisition = source.Acquisition,
                PrimaryBufferKind = cieLength > 0 ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw,
                FlipMode = source.FlipMode,
                IsMirrorReady = isMirrorReady
            };
        }

        private void CommitCalibration(LocalFrameMetadata metadata, bool rawChanged, CalibrationUpdate update)
        {
            Metadata = metadata;
            CvCieFilePath = string.Empty;
            ColorCalibration = update.Snapshot;
            CalibrationProvenance = update.Provenance;
            if (rawChanged || update.RawFlipApplied)
            {
                CvRawFilePath = string.Empty;
                SavedRawFlipApplied = null;
            }
        }

        public LocalFlowFrameLease Acquire()
        {
            lock (flipSync)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                return AcquireLease(storage, Metadata);
            }
        }

        private LocalFlowFrameLease AcquireLease(SharedRawStorage raw, LocalFrameMetadata metadata)
        {
            raw.AddReference();
            try
            {
                return new LocalFlowFrameLease(raw, cieBuffer, metadata, FrameId, MasterId, ColorCalibration, CalibrationProvenance);
            }
            catch
            {
                raw.ReleaseReference();
                throw;
            }
        }

        internal void ApplyPendingFlip(Action<LocalFlowFrameLease, LocalFrameBufferKind, CVImageFlipMode> apply)
        {
            ArgumentNullException.ThrowIfNull(apply);
            lock (flipSync)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                LocalFrameMetadata metadata = Metadata;
                LocalFrameBufferKind target = metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie
                    ? LocalFrameBufferKind.CvCie
                    : LocalFrameBufferKind.CvRaw;
                if (IsFlipApplied) return;
                if (!metadata.IsMirrorReady)
                {
                    throw new InvalidOperationException("Image mirroring is deferred until spatial calibration has completed.");
                }
                if (target == LocalFrameBufferKind.CvCie ? cieBuffer?.IsFlipFailed == true : storage.IsFlipFailed)
                {
                    throw new InvalidOperationException($"The previous {target} mirror operation failed; this frame can no longer be used safely.");
                }
                if (target == LocalFrameBufferKind.CvRaw && HasRaw && storage.HasOtherOwners)
                {
                    SharedRawStorage shared = storage;
                    storage = shared.Copy();
                    shared.ReleaseReference();
                }
                using LocalFlowFrameLease lease = Acquire();
                try
                {
                    apply(lease, target, metadata.FlipMode);
                    MarkPrimaryBufferFlipApplied();
                    if (target == LocalFrameBufferKind.CvRaw)
                    {
                        CvRawFilePath = string.Empty;
                        SavedRawFlipApplied = null;
                    }
                }
                catch
                {
                    if (target == LocalFrameBufferKind.CvCie) cieBuffer?.MarkFlipFailed();
                    else storage.MarkFlipFailed();
                    throw;
                }
            }
        }

        public void Dispose()
        {
            lock (flipSync)
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0) return;
                ReplaceCieBuffer(null);
                storage.ReleaseReference();
            }
        }

        internal sealed class CieBuffer : SafeHandleZeroOrMinusOneIsInvalid
        {
            private int flipState;

            public CieBuffer(int length) : base(ownsHandle: true)
            {
                Length = length;
                SetHandle(Marshal.AllocHGlobal(length));
            }

            public int Length { get; }
            public bool IsFlipApplied => Volatile.Read(ref flipState) == 1;
            public bool IsFlipFailed => Volatile.Read(ref flipState) < 0;
            public void MarkFlipApplied() => Volatile.Write(ref flipState, 1);
            public void MarkFlipFailed() => Volatile.Write(ref flipState, -1);

            protected override bool ReleaseHandle()
            {
                Marshal.FreeHGlobal(handle);
                return true;
            }
        }

        internal sealed class SharedRawStorage
        {
            private readonly LocalCameraRawBufferPool? rawBufferPool;
            private readonly SafeHandle? rawOwner;
            private int referenceCount = 1;
            private int rawFlipState;
            private IntPtr rawPointer;

            public SharedRawStorage(int rawLength, LocalCameraRawBufferPool? rawBufferPool,
                IntPtr cameraPixels = default, SafeHandle? rawOwner = null)
            {
                RawLength = rawLength;
                this.rawBufferPool = rawBufferPool;
                this.rawOwner = rawOwner;
                if (rawOwner != null) rawPointer = cameraPixels;
                else if (rawLength > 0) rawPointer = rawBufferPool?.Rent(rawLength) ?? Marshal.AllocHGlobal(rawLength);
            }

            ~SharedRawStorage()
            {
                FreeRaw();
            }

            public int RawLength { get; }
            public IntPtr RawPointer => rawPointer;
            public bool HasOtherOwners => Volatile.Read(ref referenceCount) > 1;
            public bool IsFlipApplied => Volatile.Read(ref rawFlipState) == 1;
            public bool IsFlipFailed => Volatile.Read(ref rawFlipState) < 0;
            public void MarkFlipApplied() => Volatile.Write(ref rawFlipState, 1);
            public void MarkFlipFailed() => Volatile.Write(ref rawFlipState, -1);

            public unsafe SharedRawStorage Copy()
            {
                // A published camera frame can be calibrated after its capture session closes.
                SharedRawStorage result = new(RawLength, null);
                Buffer.MemoryCopy((void*)RawPointer, (void*)result.RawPointer, RawLength, RawLength);
                result.rawFlipState = Volatile.Read(ref rawFlipState);
                return result;
            }

            public unsafe SharedRawStorage PromoteRaw8To16(LocalFrameMetadata source)
            {
                if (source.SourceBpp != 8 || source.Width <= 0 || source.Height <= 0 || source.Channels is not (1 or 3))
                    throw new InvalidOperationException("RAW 补零需要有效的 8 位单通道或三通道图像。");
                int samples = checked(source.Width * source.Height * source.Channels);
                if (RawPointer == IntPtr.Zero || RawLength < samples || IsFlipFailed)
                    throw new InvalidOperationException("输入 RAW 缓冲区无效，无法进行 8 位补零。");
                ReadOnlySpan<byte> input = new((void*)RawPointer, samples);
                SharedRawStorage result = new(checked(samples * sizeof(ushort)), null);
                Span<ushort> output = new((void*)result.RawPointer, samples);
                for (int i = 0; i < samples; i++) output[i] = (ushort)(input[i] << 8);
                result.rawFlipState = Volatile.Read(ref rawFlipState);
                return result;
            }

            public void RestoreSensorOrder(LocalFrameMetadata metadata)
            {
                LocalFrameMirrorService.FlipRaw(RawPointer, RawLength, metadata.Width, metadata.Height,
                    metadata.SourceBpp, metadata.Channels, metadata.FlipMode);
                Volatile.Write(ref rawFlipState, 0);
            }

            public void AddReference()
            {
                while (true)
                {
                    int current = Volatile.Read(ref referenceCount);
                    ObjectDisposedException.ThrowIf(current <= 0, nameof(LocalFlowFrame));
                    if (Interlocked.CompareExchange(ref referenceCount, current + 1, current) == current) return;
                }
            }

            public void ReleaseReference()
            {
                int remaining = Interlocked.Decrement(ref referenceCount);
                if (remaining == 0)
                {
                    FreeRaw();
                    GC.SuppressFinalize(this);
                }
                ObjectDisposedException.ThrowIf(remaining < 0, nameof(LocalFlowFrame));
            }

            private void FreeRaw()
            {
                IntPtr raw = Interlocked.Exchange(ref rawPointer, IntPtr.Zero);
                if (raw == IntPtr.Zero) return;
                if (rawOwner != null) rawOwner.Dispose();
                else if (rawBufferPool != null) rawBufferPool.Return(raw, RawLength);
                else Marshal.FreeHGlobal(raw);
            }
        }
    }

    public sealed class LocalFlowFrameLease : IDisposable
    {
        private LocalFlowFrame.SharedRawStorage? storage;
        private readonly LocalFlowFrame.CieBuffer? cieBuffer;

        internal LocalFlowFrameLease(LocalFlowFrame.SharedRawStorage storage, LocalFlowFrame.CieBuffer? cieBuffer,
            LocalFrameMetadata metadata, Guid frameId, int masterId, ColorCalibrationSnapshot? colorCalibration, ImageCalibrationSummary? calibrationProvenance)
        {
            this.storage = storage;
            this.cieBuffer = cieBuffer;
            if (cieBuffer != null)
            {
                bool retained = false;
                cieBuffer.DangerousAddRef(ref retained);
            }
            Metadata = metadata;
            FrameId = frameId;
            MasterId = masterId;
            ColorCalibration = colorCalibration;
            CalibrationProvenance = calibrationProvenance;
        }

        public Guid FrameId { get; }
        public int MasterId { get; }
        public LocalFrameMetadata Metadata { get; }
        internal ColorCalibrationSnapshot? ColorCalibration { get; }
        internal ImageCalibrationSummary? CalibrationProvenance { get; }
        public IntPtr RawPointer => GetStorage().RawPointer;
        public int RawLength => GetStorage().RawLength;
        public IntPtr CiePointer
        {
            get
            {
                _ = GetStorage();
                return cieBuffer?.DangerousGetHandle() ?? IntPtr.Zero;
            }
        }
        public int CieLength
        {
            get
            {
                _ = GetStorage();
                return cieBuffer?.Length ?? 0;
            }
        }
        public bool HasRaw => RawLength > 0;
        public bool HasCie => CieLength > 0;
        public bool IsRawFlipApplied => GetStorage().IsFlipApplied || Metadata.FlipMode == CVImageFlipMode.None;
        public bool IsCieFlipApplied => Metadata.FlipMode == CVImageFlipMode.None || cieBuffer?.IsFlipApplied == true;
        public bool IsFlipApplied => Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie ? IsCieFlipApplied : IsRawFlipApplied;

        internal bool IsBufferFlipFailed(LocalFrameBufferKind bufferKind)
            => bufferKind == LocalFrameBufferKind.CvCie
                ? cieBuffer?.IsFlipFailed == true
                : GetStorage().IsFlipFailed;

        public byte[] CopyRawToArray() => CopyToArray(RawPointer, RawLength);
        public byte[] CopyCieToArray() => CopyToArray(CiePointer, CieLength);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref storage, null) is LocalFlowFrame.SharedRawStorage current)
            {
                cieBuffer?.DangerousRelease();
                current.ReleaseReference();
            }
        }

        private LocalFlowFrame.SharedRawStorage GetStorage()
        {
            return Volatile.Read(ref storage) ?? throw new ObjectDisposedException(nameof(LocalFlowFrameLease));
        }

        private static byte[] CopyToArray(IntPtr pointer, int length)
        {
            if (pointer == IntPtr.Zero || length <= 0) return Array.Empty<byte>();
            byte[] data = new byte[length];
            Marshal.Copy(pointer, data, 0, length);
            return data;
        }
    }

    public static class LocalFlowFrameRuntime
    {
        private const string FrameResourceKeyPrefix = "ColorVision.LocalFrame.";
        public const string PoiResultResourceKeyPrefix = "ColorVision.LocalFrame.POI.";
        public const string FrameIdDataKey = "LocalFrameId";

        public static void SetCurrentFrame(this CVStartCFC action, LocalFlowFrame frame)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(frame);
            if (frame.Metadata.IsMirrorReady && !frame.IsFlipApplied)
            {
                throw new InvalidOperationException("A mirror-ready frame cannot be published before its orientation is finalized.");
            }
            action.SetRuntimeResource(GetFrameResourceKey(frame.FrameId), frame);
            action.Data[FrameIdDataKey] = frame.FrameId.ToString("N");
        }

        public static bool TryAcquireCurrentFrame(this CVStartCFC action, out LocalFlowFrameLease? lease)
        {
            lease = null;
            if (!TryGetCurrentFrame(action, out LocalFlowFrame? frame) || frame == null) return false;
            lease = frame.Acquire();
            return true;
        }

        public static bool TryGetCurrentFrame(this CVStartCFC action, out LocalFlowFrame? frame)
        {
            frame = null;
            if (!TryGetCurrentFrameId(action, out Guid frameId)) return false;
            return action.RuntimeResources.TryGet(GetFrameResourceKey(frameId), out frame);
        }

        /// <summary>
        /// Returns a file that represents the pixels and geometry of the frame's
        /// current primary buffer. The original source is safe only before any
        /// calibration or mirror transform changes that buffer.
        /// </summary>
        public static string? ResolveResultImageFilePath(this LocalFlowFrame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            string primaryFile = frame.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie
                ? frame.CvCieFilePath
                : frame.CvRawFilePath;
            if (!string.IsNullOrWhiteSpace(primaryFile)) return primaryFile;
            // A calibrated RAW replaces a locally generated CVCIE only when its
            // pixel coordinates still match the primary CIE buffer used by the algorithm.
            if (frame.ColorCalibration?.CanReplay == true && frame.IsRawFlipApplied == frame.IsCieFlipApplied
                && !string.IsNullOrWhiteSpace(frame.CvRawFilePath)) return frame.CvRawFilePath;

            bool sourceStillMatchesPrimary = frame.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvRaw
                && frame.Metadata.FlipMode == CVImageFlipMode.None
                && string.IsNullOrWhiteSpace(frame.Metadata.CalibrationTemplate);
            return sourceStillMatchesPrimary && !string.IsNullOrWhiteSpace(frame.Metadata.SourceFilePath)
                ? frame.Metadata.SourceFilePath
                : null;
        }

        public static string GetPoiResultResourceKey(Guid frameId) => PoiResultResourceKeyPrefix + frameId.ToString("N");

        private static string GetFrameResourceKey(Guid frameId) => FrameResourceKeyPrefix + frameId.ToString("N");

        private static bool TryGetCurrentFrameId(CVStartCFC action, out Guid frameId)
        {
            frameId = Guid.Empty;
            return action.Data.TryGetValue(FrameIdDataKey, out object value)
                && Guid.TryParse(value?.ToString(), out frameId);
        }
    }
}
