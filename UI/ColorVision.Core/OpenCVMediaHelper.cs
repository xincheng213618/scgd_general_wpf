using System;
using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    public class MRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public enum FocusAlgorithm 
    {
        Variance = 0,
        StandardDeviation = 1,
        Tenengrad = 2,
        Laplacian = 3,
        VarianceOfLaplacian = 4,
        EnergyOfGradient = 5,
        SpatialFrequency = 6
        // CalResol 比较复杂，通常需要特定图卡，这里不作为通用对焦算法
    };

    public static class OpenCVMediaHelper
    {
        private const string LibPath = "opencv_helper.dll";

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeResult(IntPtr str);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_FindLuminousArea(HImage img, string config, out IntPtr str);
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
        public static extern int M_DrawPoiImage(HImage image, out HImage hImage, int radius, int[] points, int pointCount, int thickness);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_ConvertImage(HImage image, out IntPtr rowGrayPixels, out int length, int scaleFactor , int targetPixelsX, int targetPixelsY);
        [DllImport(LibPath)]
        public static extern int CM_Fusion(string fusionjson, out HImage hImage);


        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void M_SetHImageData(IntPtr data);


        [DllImport(LibPath, EntryPoint = "M_CalArtculation", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double M_CalArtculation(HImage image, FocusAlgorithm  evaFunc,int roi_x, int roi_y, int roi_width, int roi_height);


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


        /// <summary>
        /// 二值化
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_Threshold(HImage image, out HImage hImage, double thresh, double maxval, int type);

        /// <summary>
        /// 滤除摩尔纹
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_RemoveMoire(HImage image, out HImage hImage);



        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_ConvertGray32Float(HImage image, out HImage hImage);



        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_StitchImages(string config, out HImage hImage);


        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public static extern int M_Fusion(string fusionjson, out HImage hImage);

        /// <summary>
        /// 高斯模糊
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <param name="kernelSize">核大小(必须为奇数)</param>
        /// <param name="sigma">标准差</param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_ApplyGaussianBlur(HImage image, out HImage hImage, int kernelSize, double sigma);

        /// <summary>
        /// 中值滤波
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <param name="kernelSize">核大小(必须为奇数)</param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_ApplyMedianBlur(HImage image, out HImage hImage, int kernelSize);

        /// <summary>
        /// 锐化
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_ApplySharpen(HImage image, out HImage hImage);

        /// <summary>
        /// Canny边缘检测
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <param name="threshold1">第一个阈值</param>
        /// <param name="threshold2">第二个阈值</param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_ApplyCannyEdgeDetection(HImage image, out HImage hImage, double threshold1, double threshold2);

        /// <summary>
        /// 直方图均衡化
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibPath, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int M_ApplyHistogramEqualization(HImage image, out HImage hImage);

    }
}
