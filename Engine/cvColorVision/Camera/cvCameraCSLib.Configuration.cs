#pragma warning disable
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraType", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCameraType(IntPtr handle, CameraType eType);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraModel", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCameraModel(IntPtr handle, CameraModel eMdl, CameraMode eMode);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetImageBpp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetImageBpp(IntPtr handle, int nBpp);

        // This legacy export returns BOOL (1/0), or a negative error for an invalid handle.
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetPortComParam", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetPortComParam(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool enabled, string port, uint baudRate);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetIndent", CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetIndent(IntPtr handle, int nL, int nT, int nR, int nB);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetFlip", CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetFlip(IntPtr handle, int flipCode);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCfgToJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetCfgToJson(IntPtr handle, ConfigType eType, StringBuilder jsonCfg, int len, [MarshalAs(UnmanagedType.I1)] bool bDefault);

        //新的修改参数
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UpdateCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int UpdateCfgJson(IntPtr handle, ConfigType eType, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoExpTime",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);
        // CV results are ordered by X/Y/Z, independently of filter-wheel traversal order.
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoExpTimeEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetAutoExpTimeEx(IntPtr handle, float[] exp, float[] saturation);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcAutoExpTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetSrcAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_Close(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SwitchCaptureMode", CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SwitchCaptureMode(IntPtr handle, TakeImageMode mode, int bpp);

    }
}
