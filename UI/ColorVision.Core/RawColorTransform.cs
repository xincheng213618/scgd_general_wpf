using System;
using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RawColorTransformV1
    {
        public uint StructSize;
        public int CalibrationType;
        public int Channels;
        public int Kind;
        public int InterleavedBgr;
        private uint Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public double[] Coefficients;

        public static RawColorTransformV1 Create() => new()
        {
            StructSize = checked((uint)Marshal.SizeOf<RawColorTransformV1>()),
            Channels = 3,
            InterleavedBgr = 1,
            Coefficients = new double[9],
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawPixelRunV1
    {
        public int Y;
        public int StartX;
        public int EndX;
    }
}
