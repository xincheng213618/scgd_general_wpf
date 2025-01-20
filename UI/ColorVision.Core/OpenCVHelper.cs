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
     

        /// <summary>
        /// 伪彩色
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="colormapTypes"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_PseudoColor(HImage image, out HImage hImage, uint min, uint max , ColormapTypes colormapTypes =ColormapTypes.COLORMAP_JET);

        /// <summary>
        /// 自动对比度
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CharSet = CharSet.Unicode)]
        public static extern int CM_AutoLevelsAdjust(HImage image, out HImage hImage);

        /// <summary>
        /// 自动颜色
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CharSet = CharSet.Unicode)]
        public static extern int CM_AutomaticColorAdjustment(HImage image, out HImage hImage);

        /// <summary>
        /// 自动色调
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CharSet = CharSet.Unicode)]
        public static extern int CM_AutomaticToneAdjustment(HImage image, out HImage hImage);


        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_ExtractChannel(HImage image, out HImage hImage, int channel);


        [DllImport(LibPath)]
        public static extern int CM_Fusion(string fusionjson, out HImage hImage);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeHImageData(IntPtr data);


    }
}
