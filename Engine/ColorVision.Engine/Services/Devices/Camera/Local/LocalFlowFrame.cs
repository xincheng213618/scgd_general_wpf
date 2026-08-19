using FlowEngineLib.Base;
using FlowEngineLib.Algorithm;
using System;
using System.Runtime.InteropServices;
using System.Threading;

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
        public int SourceBpp { get; init; }
        public int CieBpp { get; init; } = 32;
        public int Channels { get; init; }
        public float Gain { get; init; }
        public float[] Exposure { get; init; } = Array.Empty<float>();
        public string DeviceCode { get; init; } = string.Empty;
        public string SourceFilePath { get; init; } = string.Empty;
        public string CalibrationTemplate { get; init; } = string.Empty;
        public DateTime CaptureTime { get; init; } = DateTime.Now;
        public LocalFrameBufferKind PrimaryBufferKind { get; init; }
        public CVImageFlipMode FlipMode { get; init; } = CVImageFlipMode.None;
        /// <summary>Whether all sensor-coordinate transforms have completed.</summary>
        public bool IsMirrorReady { get; init; }
    }

    /// <summary>
    /// Owns the root reference to process-local RAW memory and optional CIE data memory.
    /// Consumers must use <see cref="Acquire"/> and dispose the returned lease.
    /// </summary>
    public sealed class LocalFlowFrame : IDisposable
    {
        private readonly SharedFrameStorage storage;
        private readonly object flipSync = new();
        private int disposed;

        private LocalFlowFrame(SharedFrameStorage storage, LocalFrameMetadata metadata)
        {
            this.storage = storage;
            Metadata = metadata;
            FrameId = Guid.NewGuid();
        }

        public Guid FrameId { get; }
        public LocalFrameMetadata Metadata { get; private set; }
        public int MasterId { get; set; } = -1;
        public string CvRawFilePath { get; set; } = string.Empty;
        public string CvCieFilePath { get; set; } = string.Empty;
        public bool HasRaw => storage.RawLength > 0;
        public bool HasCie => storage.CieLength > 0;
        public bool IsRawFlipApplied => storage.IsBufferFlipApplied(LocalFrameBufferKind.CvRaw, Metadata.FlipMode);
        public bool IsCieFlipApplied => storage.IsBufferFlipApplied(LocalFrameBufferKind.CvCie, Metadata.FlipMode);
        public bool IsFlipApplied => storage.IsBufferFlipApplied(Metadata.PrimaryBufferKind, Metadata.FlipMode);

        internal void MarkPrimaryBufferFlipApplied()
            => storage.MarkFlipApplied(Metadata.PrimaryBufferKind);

        public static LocalFlowFrame Allocate(LocalFrameMetadata metadata, int rawLength, int cieLength)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentOutOfRangeException.ThrowIfNegative(rawLength);
            ArgumentOutOfRangeException.ThrowIfNegative(cieLength);
            if (rawLength == 0 && cieLength == 0) throw new ArgumentException("At least one image buffer is required.");
            return new LocalFlowFrame(new SharedFrameStorage(rawLength, cieLength), metadata);
        }

        internal void PrepareForCalibration(string calibrationTemplate, int cieLength, bool hasBasicCalibration)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            storage.ResizeCieBuffer(cieLength);
            LocalFrameMetadata source = Metadata;
            Metadata = new LocalFrameMetadata
            {
                Width = source.Width,
                Height = source.Height,
                SourceBpp = source.SourceBpp,
                CieBpp = source.CieBpp,
                Channels = source.Channels,
                Gain = source.Gain,
                Exposure = source.Exposure,
                DeviceCode = source.DeviceCode,
                SourceFilePath = source.SourceFilePath,
                CalibrationTemplate = calibrationTemplate,
                CaptureTime = source.CaptureTime,
                PrimaryBufferKind = cieLength > 0 ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw,
                FlipMode = source.FlipMode,
                IsMirrorReady = true
            };
            CvCieFilePath = string.Empty;
            if (hasBasicCalibration) CvRawFilePath = string.Empty;
        }

        public LocalFlowFrameLease Acquire()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            storage.AddReference();
            return new LocalFlowFrameLease(storage, Metadata, FrameId, MasterId);
        }

        internal void ApplyPendingFlip(Action<LocalFlowFrameLease, LocalFrameBufferKind, CVImageFlipMode> apply)
        {
            ArgumentNullException.ThrowIfNull(apply);
            LocalFrameBufferKind target = Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie
                ? LocalFrameBufferKind.CvCie
                : LocalFrameBufferKind.CvRaw;
            if (storage.IsBufferFlipApplied(target, Metadata.FlipMode)) return;
            if (!Metadata.IsMirrorReady)
            {
                throw new InvalidOperationException("Image mirroring is deferred until spatial calibration has completed.");
            }

            lock (flipSync)
            {
                if (storage.IsBufferFlipApplied(target, Metadata.FlipMode)) return;
                if (storage.IsBufferFlipFailed(target))
                {
                    throw new InvalidOperationException($"The previous {target} mirror operation failed; this frame can no longer be used safely.");
                }
                using LocalFlowFrameLease lease = Acquire();
                try
                {
                    apply(lease, target, Metadata.FlipMode);
                    storage.MarkFlipApplied(target);
                }
                catch
                {
                    storage.MarkFlipFailed(target);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                storage.ReleaseReference();
            }
        }

        internal sealed class SharedFrameStorage
        {
            private readonly object bufferSync = new();
            private int referenceCount = 1;
            private int rawFlipState;
            private int cieFlipState;
            private int cieLength;
            private IntPtr rawPointer;
            private IntPtr ciePointer;

            public SharedFrameStorage(int rawLength, int cieLength)
            {
                RawLength = rawLength;
                this.cieLength = cieLength;
                try
                {
                    if (rawLength > 0) rawPointer = Marshal.AllocHGlobal(rawLength);
                    if (cieLength > 0) ciePointer = Marshal.AllocHGlobal(cieLength);
                }
                catch
                {
                    FreeBuffers();
                    throw;
                }
            }

            ~SharedFrameStorage()
            {
                FreeBuffers();
            }

            public int RawLength { get; }
            public int CieLength => Volatile.Read(ref cieLength);
            public IntPtr RawPointer => rawPointer;
            public IntPtr CiePointer => ciePointer;

            public void ResizeCieBuffer(int requiredLength)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(requiredLength);
                lock (bufferSync)
                {
                    if (requiredLength == cieLength) return;
                    IntPtr replacement = requiredLength > 0 ? Marshal.AllocHGlobal(requiredLength) : IntPtr.Zero;
                    IntPtr previous = ciePointer;
                    ciePointer = replacement;
                    Volatile.Write(ref cieLength, requiredLength);
                    Volatile.Write(ref cieFlipState, 0);
                    if (previous != IntPtr.Zero) Marshal.FreeHGlobal(previous);
                }
            }

            public bool IsBufferFlipApplied(LocalFrameBufferKind bufferKind, CVImageFlipMode flipMode)
                => flipMode == CVImageFlipMode.None || Volatile.Read(ref GetFlipState(bufferKind)) == 1;

            public bool IsBufferFlipFailed(LocalFrameBufferKind bufferKind)
                => Volatile.Read(ref GetFlipState(bufferKind)) < 0;

            public void MarkFlipApplied(LocalFrameBufferKind bufferKind)
                => Volatile.Write(ref GetFlipState(bufferKind), 1);

            public void MarkFlipFailed(LocalFrameBufferKind bufferKind)
                => Volatile.Write(ref GetFlipState(bufferKind), -1);

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
                    FreeBuffers();
                }
                ObjectDisposedException.ThrowIf(remaining < 0, nameof(LocalFlowFrame));
            }

            private void FreeBuffers()
            {
                lock (bufferSync)
                {
                    IntPtr raw = Interlocked.Exchange(ref rawPointer, IntPtr.Zero);
                    if (raw != IntPtr.Zero) Marshal.FreeHGlobal(raw);
                    IntPtr cie = Interlocked.Exchange(ref ciePointer, IntPtr.Zero);
                    if (cie != IntPtr.Zero) Marshal.FreeHGlobal(cie);
                    Volatile.Write(ref cieLength, 0);
                }
            }

            private ref int GetFlipState(LocalFrameBufferKind bufferKind)
            {
                if (bufferKind == LocalFrameBufferKind.CvCie) return ref cieFlipState;
                return ref rawFlipState;
            }
        }
    }

    public sealed class LocalFlowFrameLease : IDisposable
    {
        private LocalFlowFrame.SharedFrameStorage? storage;

        internal LocalFlowFrameLease(LocalFlowFrame.SharedFrameStorage storage, LocalFrameMetadata metadata, Guid frameId, int masterId)
        {
            this.storage = storage;
            Metadata = metadata;
            FrameId = frameId;
            MasterId = masterId;
        }

        public Guid FrameId { get; }
        public int MasterId { get; }
        public LocalFrameMetadata Metadata { get; }
        public IntPtr RawPointer => GetStorage().RawPointer;
        public int RawLength => GetStorage().RawLength;
        public IntPtr CiePointer => GetStorage().CiePointer;
        public int CieLength => GetStorage().CieLength;
        public bool HasRaw => RawLength > 0;
        public bool HasCie => CieLength > 0;
        public bool IsRawFlipApplied => GetStorage().IsBufferFlipApplied(LocalFrameBufferKind.CvRaw, Metadata.FlipMode);
        public bool IsCieFlipApplied => GetStorage().IsBufferFlipApplied(LocalFrameBufferKind.CvCie, Metadata.FlipMode);
        public bool IsFlipApplied => GetStorage().IsBufferFlipApplied(Metadata.PrimaryBufferKind, Metadata.FlipMode);

        internal bool IsBufferFlipFailed(LocalFrameBufferKind bufferKind)
            => GetStorage().IsBufferFlipFailed(bufferKind);

        internal void MarkBufferFlipApplied(LocalFrameBufferKind bufferKind)
            => GetStorage().MarkFlipApplied(bufferKind);

        public byte[] CopyRawToArray() => CopyToArray(RawPointer, RawLength);
        public byte[] CopyCieToArray() => CopyToArray(CiePointer, CieLength);

        public void Dispose()
        {
            Interlocked.Exchange(ref storage, null)?.ReleaseReference();
        }

        private LocalFlowFrame.SharedFrameStorage GetStorage()
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
            action.RuntimeResources.Set(GetFrameResourceKey(frame.FrameId), frame);
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
