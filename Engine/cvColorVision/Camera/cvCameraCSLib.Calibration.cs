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
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatCameraManagerEx",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CreatCameraManagerEx(CameraType eType, string cfgFilename);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseCameraManager",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ReleaseCameraManager(IntPtr handle);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraType",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_SetCameraType(IntPtr handle, CameraType eType);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraModel",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_SetCameraModel(IntPtr handle, CameraModel eMdl, CameraMode eMode);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatCameraManager",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CreatCameraManager(CameraType eType, string CameraID, string cfgFilename);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetImageBpp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetImageBpp(IntPtr handle, int nBpp);
        //新建一个校正用的句柄
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CreatCalibrationManage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CreatCalibrationManage();
        //释放校正用的句柄
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ReleaseCalibrationManage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool ReleaseCalibrationManage(IntPtr intPtr);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetIndent", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CM_SetIndent(IntPtr handle, int nL, int nT, int nR, int nB);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetFlip", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CM_SetFlip(IntPtr handle, int flipCode);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCalibParam", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCalibParam(IntPtr handle, CalibrationType cType, bool bEnabled, string filename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDarkNoise", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDarkNoise(IntPtr handle, bool bEnabled, float darkNoiseRatio);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDefectWPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDefectWPoint(IntPtr handle, bool bEnabled, int[] pX, int[] pY, int nLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDefectBPoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDefectBPoint(IntPtr handle, bool bEnabled, int[] pX, int[] pY, int nLen);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDefectPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDefectPoint(IntPtr handle, bool bEnabled, int[] pX, int[] pY, int nLen);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDSNU", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDSNU(IntPtr handle, bool bEnabled, uint dsnuWidth, uint dsnuHeight, ushort[] pdsnuData);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamUniformity", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamUniformity(IntPtr handle, bool bEnabled, uint UniftyWidth, uint UniftyHeight, float[] pUniftyData);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDistortion", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDistortion(IntPtr handle, bool bEnabled, SIZE tImgSize, float[] cameraMatrix, float[] distCoeffs, float alpha, bool buseFisheye);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamLuminance",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamLuminance(IntPtr handle, bool bEnabled, float[] Texp, float[] Gain, float[] pa);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorOne", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorOne(IntPtr handle, bool bEnabled, float[] Texp, float[] Gain, float[] pa);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorFour", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorFour(IntPtr handle, bool bEnabled, float[] Texp, float[] Gain, float[] pa);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorMulti", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorMulti(IntPtr handle, bool bEnabled, float[] pa, float[] gain, int N);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorShift", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorShift(IntPtr handle, bool bEnabled, int OffX, int OffY, bool FillOffset);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DarkNoise", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DarkNoise(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);       
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectWPoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DefectWPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectBPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DefectBPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectPoint",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DefectPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Uniformity",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_Uniformity(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DSNU",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DSNU(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorShift",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorShift(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Distortion",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_Distortion(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Luminance", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_Luminance(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata  , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorOne", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorOne(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorOneEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorOneEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy , byte[] srcz, byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorFour", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorFour(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorFourEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorFourEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy , byte[] srcz, byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorMulti",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorMulti(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorMultiEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorMultiEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy  , byte[] srcz, byte[] dstimgdata, float[] dexp);
    }
}
