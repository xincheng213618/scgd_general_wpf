#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace cvColorVision;

[StructLayout(LayoutKind.Sequential)]
public struct CameraRawFrameLeaseV1
{
    public const uint PackedBgr16 = 1;
    public const int UnsupportedBeforeCapture = 0;

    public uint StructSize;
    public uint Width;
    public uint Height;
    public uint BitsPerChannel;
    public uint Channels;
    public uint Format;
    public ulong ByteLength;
    public IntPtr Pixels;
    public IntPtr Owner;
}

public partial class cvCameraCSLib
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int AcquireRawFrameV1(IntPtr cameraHandle, string captureJson, ref CameraRawFrameLeaseV1 frame);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ReleaseRawFrameV1(IntPtr owner);

    private static readonly Lazy<(AcquireRawFrameV1? Acquire, ReleaseRawFrameV1? Release)> RawFrameLeaseExports
        = new(FindRawFrameLeaseExports);

    /// <summary>
    /// Gets the optional acquisition and matching release from the module that
    /// resolved the camera calls. Open the camera before querying this API.
    /// </summary>
    public static bool TryGetRawFrameLeaseV1(
        [NotNullWhen(true)] out AcquireRawFrameV1? acquire,
        [NotNullWhen(true)] out ReleaseRawFrameV1? release)
    {
        (acquire, release) = CameraNativeLibrary.LoadedModule == IntPtr.Zero
            ? default
            : RawFrameLeaseExports.Value;
        return acquire != null && release != null;
    }

    private static (AcquireRawFrameV1?, ReleaseRawFrameV1?) FindRawFrameLeaseExports()
    {
        IntPtr module = CameraNativeLibrary.LoadedModule;
        if (!NativeLibrary.TryGetExport(module, "CM_AcquireRawFrameV1", out IntPtr acquire)
            || !NativeLibrary.TryGetExport(module, "CM_ReleaseRawFrameV1", out IntPtr release))
            return default;

        return (Marshal.GetDelegateForFunctionPointer<AcquireRawFrameV1>(acquire),
            Marshal.GetDelegateForFunctionPointer<ReleaseRawFrameV1>(release));
    }
}
