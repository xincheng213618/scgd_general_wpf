#pragma warning disable
using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetCallBack",  CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetCallBack(IntPtr handle, QHYCCDProcCallBack callback, IntPtr obj);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_UnregisterCallBack", CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_UnregisterCallBack(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int CM_GetSrcFrame_Gen(IntPtr handle, ref uint w, ref uint h,ref uint bpp, ref uint channels, byte[] rawArray);

        public static int CM_GetSrcFrame(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels, ref byte[] rawArray)
        {
            int infoResult = unchecked((int)CM_GetSrcFrameInfo(handle, ref w, ref h, ref bpp, ref channels));
            if (infoResult <= 0)
            {
                rawArray = Array.Empty<byte>();
                return infoResult;
            }
            uint nbbpMen = bpp / 8;
            rawArray = new byte[checked((int)((ulong)w * h * nbbpMen * channels))];
            return CM_GetSrcFrame_Gen(handle, ref w, ref h, ref bpp, ref channels, rawArray);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrameEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int CM_GetSrcFrameEx_Gen(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels, byte[] rawArray);
        public static int CM_GetSrcFrameEx(IntPtr handle, ref uint w, ref uint h,  ref uint bpp, ref uint channels, ref byte[] rawArray)
        {
            int infoResult = unchecked((int)CM_GetSrcFrameInfo(handle, ref w, ref h, ref bpp, ref channels));
            if (infoResult <= 0)
            {
                rawArray = Array.Empty<byte>();
                return infoResult;
            }
            uint nbbpMen = bpp / 8;
            rawArray = new byte[checked((int)((ulong)w * h * nbbpMen * channels))];
            return CM_GetSrcFrameEx_Gen(handle, ref w, ref h, ref bpp, ref channels, rawArray);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrameInfo", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern uint CM_GetSrcFrameInfo(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetCfwport",CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetCfwport(IntPtr handle, int nIndex, int nPort, ImageChannelType eImgChlType);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ImageRect",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ImageRect(int w, int h, int bpp, int channels, byte[] imgdata, IRECT tIRECT, byte[] imgDstdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SkipTake",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int SkipTake(byte[] psrcdata, byte[] pdstdata, int nPos, int nCount);

        public static int SkipTake(byte[] psrcdata, ref byte[] pdstdata, int nPos, int nCount)
        {
            ArgumentNullException.ThrowIfNull(psrcdata);
            if (nPos < 0 || nCount < 0 || nPos > psrcdata.Length - nCount) throw new ArgumentOutOfRangeException(nameof(nPos));
            pdstdata = new byte[nCount];
            return SkipTake(psrcdata, pdstdata, nPos, nCount);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetAutoFocusExposure", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetAutoFocusExposure(IntPtr handle, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CalcAutoFocus",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_CalcAutoFocus(IntPtr handle, AutoFocusCfg tAtFsCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoFocus", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetAutoFocus(IntPtr handle, ref int nPos);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_AutoFocusCallBack", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_AutoFocusCallBack(IntPtr handle, AutoFocusCallBack CallBackFun, IntPtr usrData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int AutoFocusCallBack(IntPtr usrData, int nW, int nH, int bpp, int channels, IntPtr pData, int Pos, double evalua);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CalDistanceEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CalDistanceEx(int nPos, ref double dDis);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraROI", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCameraROI(IntPtr handle, UInt32 ex, UInt32 ey, UInt32 ew, UInt32 eh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraROI",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetCameraROI(IntPtr handle, ref UInt32 ex, ref UInt32 ey, ref UInt32 ew, ref UInt32 eh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvDisplayImage",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int cvDisplayImage(IntPtr hWnd, CRECT rt, CVImage iImg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFit",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int cvCalcFit(int nFit, double[] arrX, double[] arrY, int nDataSize);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateFit",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_CreateFit(int id, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetFitFy",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int cvGetFitFy(double dXvalue, ref double dYvalue);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFitFy", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetFitFy(int id, double dXvalue, ref double dYvalue);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFiveDot",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int cvCalcFiveDot(CVImage iImg, double[] x, double[] y, int nThreshold);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseFit",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_ReleaseFit(int nId);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveRelPostion",    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int MovePostion(IntPtr handle, int nPosition, uint dwTimeOut);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPosition",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GetPosition(IntPtr handle, ref int nPosition, uint dwTimeOut);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GoHome",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GoHome(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ShutDown", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ShutDown(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitCom",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int InitCom(IntPtr handle, FOCUS_COMMUN eFOCUS_COMMUN, string ComName, uint BaudRate);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int IsOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveDiaphragm",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int MoveDiaphragm(IntPtr handle, float dPosition, uint dwTimeOut);

    }
}
