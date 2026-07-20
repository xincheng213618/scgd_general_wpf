using FlowEngineLib.Base;
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
    }

    /// <summary>
    /// Owns the root reference to a pair of process-local unmanaged image buffers.
    /// Consumers must use <see cref="Acquire"/> and dispose the returned lease.
    /// </summary>
    public sealed class LocalFlowFrame : IDisposable
    {
        private readonly SharedFrameStorage storage;
        private int disposed;

        private LocalFlowFrame(SharedFrameStorage storage, LocalFrameMetadata metadata)
        {
            this.storage = storage;
            Metadata = metadata;
            FrameId = Guid.NewGuid();
        }

        public Guid FrameId { get; }
        public LocalFrameMetadata Metadata { get; }
        public int MasterId { get; set; } = -1;
        public string CvRawFilePath { get; set; } = string.Empty;
        public string CvCieFilePath { get; set; } = string.Empty;
        public bool HasRaw => storage.RawLength > 0;
        public bool HasCie => storage.CieLength > 0;

        public static LocalFlowFrame Allocate(LocalFrameMetadata metadata, int rawLength, int cieLength)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentOutOfRangeException.ThrowIfNegative(rawLength);
            ArgumentOutOfRangeException.ThrowIfNegative(cieLength);
            if (rawLength == 0 && cieLength == 0) throw new ArgumentException("At least one image buffer is required.");
            return new LocalFlowFrame(new SharedFrameStorage(rawLength, cieLength), metadata);
        }

        public LocalFlowFrameLease Acquire()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            storage.AddReference();
            return new LocalFlowFrameLease(storage, Metadata, FrameId, MasterId);
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
            private int referenceCount = 1;
            private IntPtr rawPointer;
            private IntPtr ciePointer;

            public SharedFrameStorage(int rawLength, int cieLength)
            {
                RawLength = rawLength;
                CieLength = cieLength;
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
            public int CieLength { get; }
            public IntPtr RawPointer => rawPointer;
            public IntPtr CiePointer => ciePointer;

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
                IntPtr raw = Interlocked.Exchange(ref rawPointer, IntPtr.Zero);
                if (raw != IntPtr.Zero) Marshal.FreeHGlobal(raw);
                IntPtr cie = Interlocked.Exchange(ref ciePointer, IntPtr.Zero);
                if (cie != IntPtr.Zero) Marshal.FreeHGlobal(cie);
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
