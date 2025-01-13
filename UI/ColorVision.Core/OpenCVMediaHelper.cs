#pragma warning disable CA1401,CA1051,CA2101,CA1707
using System;
using System.Runtime.InteropServices;

namespace ColorVision
{
    public static class OpenCVMediaHelper
    {
        private const string LibPath = "opencv_helper.dll";


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
        public static extern int M_PseudoColor(HImage image, out HImage hImage, uint min, uint max, ColormapTypes colormapTypes ,int channel);

        /// <summary>
        /// 自动对比度
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CharSet = CharSet.Unicode)]
        public static extern int M_AutoLevelsAdjust(HImage image, out HImage hImage);

        /// <summary>
        /// 自动颜色
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CharSet = CharSet.Unicode)]
        public static extern int M_AutomaticColorAdjustment(HImage image, out HImage hImage);

        /// <summary>
        /// 自动色调
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CharSet = CharSet.Unicode)]
        public static extern int M_AutomaticToneAdjustment(HImage image, out HImage hImage);


        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_ExtractChannel(HImage image, out HImage hImage, int channel);
       
        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_DrawPoiImage(HImage image, out HImage hImage, int radio, int[] points, int pointCount, int thickness);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_ConvertImage(HImage image, out IntPtr rowGrayPixels, out int length, int scaleFactor , int targetPixelsX, int targetPixelsY);
        [DllImport(LibPath)]
        public static extern int CM_Fusion(string fusionjson, out HImage hImage);


        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void M_FreeHImageData(IntPtr data);


        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void M_SetHImageData(IntPtr data);
        public enum EvaFunc
        {
            Variance = 0,
            Tenengrad = 1,
            Laplace,
            CalResol,
        };

        [DllImport(LibPath, EntryPoint = "M_CalArtculation", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double M_CalArtculation(HImage image, EvaFunc evaFunc);


        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_GetWhiteBalance(HImage image, out HImage hImage, double redBalance, double greenBalance, double blueBalance);

        /// <summary>
        /// 伽马校正
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <param name="gamma"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_ApplyGammaCorrection(HImage image, out HImage hImage, double gamma);

        /// <summary>
        /// 调整亮度对比度
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <param name="alpha"></param>
        /// <param name="beta"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_AdjustBrightnessContrast(HImage image, out HImage hImage, double alpha, double beta);

        /// <summary>
        /// 反相
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_InvertImage(HImage image, out HImage hImage);

    }
}
