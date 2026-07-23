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
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCfgToJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_GetCfgToJson(IntPtr handle, ConfigType eType, StringBuilder jsonCfg, int len, bool bDefault);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetSysCfgJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetSysCfgJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);

        public static string GetSysCfgJson(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            GetSysCfgJson_Gen(handle, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultSysCfgJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultSysCfgJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultSysCfgJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            GetDefaultSysCfgJson_Gen(handle, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultCameraCfgToJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultCameraCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultCameraCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            CM_GetCfgToJson(handle, ConfigType.Cfg_Camera, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultExpTimeCfgToJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultExpTimeCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultExpTimeCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            CM_GetCfgToJson(handle, ConfigType.Cfg_ExpTime, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultCaliLibCfgToJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultCaliLibCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultCaliLibCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            CM_GetCfgToJson(handle, ConfigType.Cfg_Calibration, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultChannelsCfgToJson",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultChannelsCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);

        public static string GetDefaultChannelsCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            GetDefaultChannelsCfgToJson_Gen(handle, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateCaliLibCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateCaliLibCfgJson(IntPtr handle, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateCameraCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateCameraCfgJson(IntPtr handle, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateChannelCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateChannelCfgJson(IntPtr handle, string jsonCfg);

        //新的修改参数
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UpdateCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public unsafe static extern bool UpdateCfgJson(IntPtr handle, ConfigType eType, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateExpTimeCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateExpTimeCfgJson(IntPtr handle, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoExpTime",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcAutoExpTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetSrcAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_Close(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LiveClose", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_LiveClose(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateSysCfgJson",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateSysCfgJson(IntPtr handle, string jsonPm);
    }
}
