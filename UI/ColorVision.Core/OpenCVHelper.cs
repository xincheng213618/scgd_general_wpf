#pragma warning disable CA1401,CA1051,CA2101,CA1707
using System;
using System.Runtime.InteropServices;

namespace ColorVision
{
    public static class OpenCVHelper
    {
        private const string LibPath = "opencv_helper.dll";

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ReadGhostImage([MarshalAs(UnmanagedType.LPStr)] string FilePath, int singleLedPixelNum, int[] LEDPixelX, int[] LEDPixelY, int singleGhostPixelNum, int[] GhostPixelX, int[] GhostPixelY, out HImage hImage);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GhostImage(HImage image, out HImage hImage, int singleLedPixelNum, int[] LEDPixelX, int[] LEDPixelY, int singleGhostPixelNum, int[] GhostPixelX, int[] GhostPixelY);
     

    }
}
