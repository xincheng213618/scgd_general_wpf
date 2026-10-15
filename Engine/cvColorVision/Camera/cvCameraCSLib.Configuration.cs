#pragma warning disable
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCfgToJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetCfgToJson(IntPtr handle, ConfigType eType, StringBuilder jsonCfg, int len, [MarshalAs(UnmanagedType.I1)] bool bDefault);

        //新的修改参数
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UpdateCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int UpdateCfgJson(IntPtr handle, ConfigType eType, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoExpTime",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcAutoExpTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetSrcAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_Close(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SwitchCaptureMode", CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SwitchCaptureMode(IntPtr handle, TakeImageMode mode, int bpp);

    }
}
