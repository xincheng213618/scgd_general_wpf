#pragma warning disable
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetDeviceOnline", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetDeviceOnline(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetTakeImageMode", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetTakeImageMode(IntPtr handle, TakeImageMode eTakeImageMode);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Open",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_Open(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetGain", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetGain(IntPtr handle, float fGain);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetExpTime",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetExpTime(IntPtr handle, float expTime);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetExpTimeEx",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetExpTimeEx(IntPtr handle, int index, float expTime);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParam", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetParam(IntPtr handle, int pid, double val);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsFeatureAvailable",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsFeatureAvailable_Gen(IntPtr handle, int pid);
        public static bool CM_IsFeatureAvailable(IntPtr handle, int pid) => CM_IsFeatureAvailable_Gen(handle, pid);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sleep", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_Sleep(float ms);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetPort",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetPort(IntPtr handle, int port);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsBurstmodeAvailable",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsBurstmodeAvailable(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame_TIFF",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetFrame_TIFF(IntPtr handle, string jsonPm);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int CM_GetAllCameraIDMD5(StringBuilder jsonCfg, int strLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetLiveFrame",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetLiveFrame(IntPtr handle, ref uint w, ref uint h, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetFrame(IntPtr handle, string jsonPm, ref uint w, ref uint h, ref uint srcbpp, ref uint bpp, ref uint channels, byte[] srcrawArray, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetFrame(IntPtr handle, string jsonPm, ref uint w, ref uint h, ref uint srcbpp, ref uint bpp, ref uint channels, IntPtr srcrawBuffer, IntPtr cieBuffer);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetAutoExpFrame(IntPtr handle, ref uint width, ref uint height, ref uint bpp, ref uint channels,
            IntPtr imageBuffer, ulong imageCapacity, [Out] float[] exposure, [Out] float[] saturation);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ExportToTIFF", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_ExportToTIFF(string fileName, uint w, uint h, uint bpp, uint channels, byte[] rawArray);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ExportToTIFFEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_ExportToTIFFEx(string fileName, uint w, uint h, uint bpp, uint channels, byte[] rawArray, double dRate);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetDeviceMode",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int C_CM_GetDeviceMode(IntPtr handle, StringBuilder mode, int len);
        public static string CM_GetDeviceMode(IntPtr handle)
        {
            StringBuilder builder = new StringBuilder(1024);
            int result = C_CM_GetDeviceMode(handle, builder, builder.Capacity);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw NativeError("CM_GetDeviceMode", result);
            return builder.ToString();
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSN",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int CM_GetSN(IntPtr handle, StringBuilder sn, int len);

        public static string CM_GetSN(IntPtr handle)
        {
            StringBuilder builder = new StringBuilder(1024);
            int result = CM_GetSN(handle, builder, builder.Capacity);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw NativeError("CM_GetSN", result);
            return builder.ToString();
        }
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SplitData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SplitData(uint w, uint h, uint bpp, uint channels, byte[] imgdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SplitDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SplitDataEx(int w, int h, int bpp, byte[] srcimgdata, byte[] dstX, byte[] dstY, byte[] dstZ);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_MergeData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_MergeData(uint w, uint h, uint bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_MergeDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_MergeDataEx(uint w, uint h, uint bpp, byte[] imgdata, byte[] srcX, byte[] srcY, byte[] srcZ);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern uint CM_GetFrameMemLength(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameMaxMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern ulong CM_GetFrameMaxMemLength(IntPtr handle);
    }
}
