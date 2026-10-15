#pragma warning disable
using System;
using System.Runtime.InteropServices;

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
        public unsafe static extern int CM_SetCameraType(IntPtr handle, CameraType eType);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraModel",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCameraModel(IntPtr handle, CameraModel eMdl, CameraMode eMode);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatCameraManager",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CreatCameraManager(CameraType eType, string CameraID, string cfgFilename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetImageBpp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetImageBpp(IntPtr handle, int nBpp);
        //新建一个校正用的句柄
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CreatCalibrationManage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CreatCalibrationManage();
        //释放校正用的句柄
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ReleaseCalibrationManage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ReleaseCalibrationManage(IntPtr intPtr);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetIndent", CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetIndent(IntPtr handle, int nL, int nT, int nR, int nB);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetFlip", CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetFlip(IntPtr handle, int flipCode);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCalibParam", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCalibParam(IntPtr handle, CalibrationType cType, [MarshalAs(UnmanagedType.I1)] bool bEnabled, string filename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitCalibration", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_InitCalibration(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UnInitCalibration", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_UnInitCalibration(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LoadItemV1", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_LoadItemV1(IntPtr handle, CalibrationType calibrationType, string title, string filename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SelectItemV1", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SelectItemV1(IntPtr handle, CalibrationType calibrationType, string title);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ClearSelectItemV1", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_ClearSelectItemV1(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_RoutineCalibrationV1", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_RoutineCalibrationV1(IntPtr handle, uint width, uint height, uint bpp, uint channels, IntPtr imageData);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_TransformV1", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_TransformV1(IntPtr handle, uint width, uint height, uint bpp, uint channels, IntPtr sourceData, IntPtr destinationData, float[] exposure);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DarkNoise", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DarkNoise(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata, float[] exposure);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DarkNoise", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DarkNoise(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata, float[] exposure);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectWPoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DefectWPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectWPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DefectWPoint(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectBPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DefectBPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectBPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DefectBPoint(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectPoint",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DefectPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DefectPoint(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Uniformity",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_Uniformity(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Uniformity", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_Uniformity(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DSNU",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DSNU(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DSNU", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_DSNU(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorShift",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorShift(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorShift", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorShift(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Distortion",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_Distortion(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Distortion", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_Distortion(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        // Native exports are implemented in the associated project:
        // scgd_internal_dll/cvCameraItem/cvCameraItem/cvCalibration.{h,cpp}.
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_LineArity", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_LineArity(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorDiff", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorDiff(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_AngleShift", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_AngleShift(IntPtr handle, int w, int h, int bpp, uint channels, IntPtr imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Luminance", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_Luminance(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata  , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Luminance", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_Luminance(IntPtr handle, uint w, uint h, int bpp, uint channels, IntPtr srcimgdata, IntPtr dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorOne", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorOne(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorOne", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorOne(IntPtr handle, uint w, uint h, int bpp, uint channels, IntPtr srcimgdata, IntPtr dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorOneEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorOneEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy , byte[] srcz, byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorFour", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorFour(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorFour", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorFour(IntPtr handle, uint w, uint h, int bpp, uint channels, IntPtr srcimgdata, IntPtr dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorFourEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorFourEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy , byte[] srcz, byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorMulti",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorMulti(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorMulti", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorMulti(IntPtr handle, uint w, uint h, int bpp, uint channels, IntPtr srcimgdata, IntPtr dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorMultiEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SCGD_SDP_ColorMultiEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy  , byte[] srcz, byte[] dstimgdata, float[] dexp);
    }
}
