#pragma warning disable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetCallBack",  CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CM_SetCallBack(IntPtr handle, QHYCCDProcCallBack callback, IntPtr obj);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_UnregisterCallBack", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CM_UnregisterCallBack(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool CM_GetSrcFrame_Gen(IntPtr handle, ref uint w, ref uint h,ref uint bpp, ref uint channels, byte[] rawArray);

        public static bool CM_GetSrcFrame(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels, ref byte[] rawArray)
        {
            _=CM_GetSrcFrameInfo(handle, ref w, ref h, ref bpp, ref channels);
            uint nbbpMen = bpp / 8;
            rawArray = new byte[w * h * nbbpMen * channels];
            return CM_GetSrcFrame_Gen(handle, ref w, ref h, ref bpp, ref channels, rawArray);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrameEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool CM_GetSrcFrameEx_Gen(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels, byte[] rawArray);
        public static bool CM_GetSrcFrameEx(IntPtr handle, ref uint w, ref uint h,  ref uint bpp, ref uint channels, ref byte[] rawArray)
        {
            _=CM_GetSrcFrameInfo(handle, ref w, ref h, ref bpp, ref channels);
            uint nbbpMen = bpp / 8;
            rawArray = new byte[w * h * nbbpMen * channels];
            return CM_GetSrcFrameEx_Gen(handle, ref w, ref h, ref bpp, ref channels, rawArray);
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrameInfo", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern uint CM_GetSrcFrameInfo(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetCfwport",CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CM_SetCfwport(IntPtr handle, int nIndex, int nPort, ImageChannelType eImgChlType);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ImageRect",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void ImageRect(int w, int h, int bpp, int channels, byte[] imgdata, IRECT tIRECT, byte[] imgDstdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SkipTake",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void SkipTake(byte[] psrcdata, byte[] pdstdata, int nPos, int nCount);

        public static void SkipTake(byte[] psrcdata, ref byte[] pdstdata, int nPos, int nCount)
        {
            pdstdata = new byte[nCount];
            SkipTake(psrcdata, pdstdata, nPos, nCount);
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpenVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool IsOpenVid();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetAutoFocusShowWnd",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetAutoFocusShowWnd(IntPtr handle, IntPtr hWnd, CRECT rt);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetAutoFocusExposure", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetAutoFocusExposure(IntPtr handle, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CalcAutoFocus",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_CalcAutoFocus(IntPtr handle, AutoFocusCfg tAtFsCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalArticulation", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double cvCalArticulation(EvaFunc type, CVImage iImg, int dx = 0, int dy = 1, int ksize = 5, double dRatio = 0.01);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoFocus", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetAutoFocus(IntPtr handle, ref int nPos);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_AutoFocusCallBack", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_AutoFocusCallBack(IntPtr handle, AutoFocusCallBack CallBackFun, IntPtr usrData);

        public delegate int AutoFocusCallBack(IntPtr usrData, int nW, int nH, int bpp, int channels, IntPtr pData, int Pos, double evalua);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CalDistanceEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CalDistanceEx(int nPos, ref double dDis);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MovePostionVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MovePostionVid(int nPosition, bool bdirection, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPostionVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GetPostionVid(ref int nPosition, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitComCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool InitComCanon(string ComName, uint BaudRate);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpenCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool IsOpenCanon();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ShutDownCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool ShutDownCanon();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MovePostionCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MovePostionCanon(int nPosition, uint dwTimeOut);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraROI", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCameraROI(IntPtr handle, UInt32 ex, UInt32 ey, UInt32 ew, UInt32 eh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraROI",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetCameraROI(IntPtr handle, ref UInt32 ex, ref UInt32 ey, ref UInt32 ew, ref UInt32 eh);



        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPostionCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GetPostionCanon(ref int nPosition, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvDisplayImage",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvDisplayImage(IntPtr hWnd, CRECT rt, CVImage iImg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFit",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvCalcFit(int nFit, double[] arrX, double[] arrY, int nDataSize);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateFit",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_CreateFit(int id, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetFitFy",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvGetFitFy(double dXvalue, ref double dYvalue);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFitFy", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetFitFy(int id, double dXvalue, ref double dYvalue);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFiveDot",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvCalcFiveDot(CVImage iImg, double[] x, double[] y, int nThreshold);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "getCirclePoint",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool getCirclePoint(uint w, uint h, uint bpp, uint channels, byte[] imgData, int thresholdValue, ref float x, ref float y);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "FovImgCentre",    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool FovImgCentre(CVImage tImg, double Radio, double cameraDegrees, ref double fovDegrees, int thresholdValus, double dFovDist, FovPattern pattern, FovType type);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SFRCalculation",     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int SFRCalculation(CVImage tImg, CRECT rtROI, double gamma, float[] pdfrequency, float[] pdomainSamplingData, int nLen);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "getCirclePoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool getCirclePoint(CVImage matsrc, int thresholdValue, ref System.Windows.Point tPt);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SetMachineVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void SetMachineVid(int ucMachineNO);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseFit",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ReleaseFit(int nId);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveRelPostion",    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MovePostion(IntPtr handle, int nPosition, uint dwTimeOut);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPosition",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GetPosition(IntPtr handle, ref int nPosition, uint dwTimeOut);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GoHome",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GoHome(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ShutDown", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool ShutDown(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitCom",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool InitCom(IntPtr handle, FOCUS_COMMUN eFOCUS_COMMUN, string ComName, uint BaudRate);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool IsOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveDiaphragm",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MoveDiaphragm(IntPtr handle, float dPosition, uint dwTimeOut);

        //[DllImport(LIBRARY_CVCAMERA, EntryPoint = "DistortionCheck",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        //public unsafe static extern int DistortionCheck(CVImage tImg, SIZE iSize, BlobThreParams tBlobThreParams, float[] finalPointsX, float[] finalPointsY, ref double pointx, ref double pointy, ref double maxErrorRatio, ref double t, CornerType type /*= Circlepoint*/, SlopeType sType /*= CenterPoint*/, LayoutType lType /*= SlopeIN*/);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "FovImgCentreEX",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool FovImgCentreEX(CVImage tImg, float x_c, float y_c, float x_p, float y_p, double Radio, double cameraDegrees, ref double fovDegrees, int thresholdValus, double dFovDist, FovPattern pattern, FovType type);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GhostGlareDectect",
        CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GhostGlareDectect(CVImage tImg, int radius, int cols, int rows, float ratioH, float ratioL, string path, float[] centersX, float[] centersY, float[] blobGray, float[] dstGray, ref int memSizeH, ref int numArrH, int[] arrH, int[] dataH_X, int[] dataH_Y, ref int memSizeL, ref int numArrL, int[] arrL, int[] dataL_X, int[] dataL_Y);
    }
}
