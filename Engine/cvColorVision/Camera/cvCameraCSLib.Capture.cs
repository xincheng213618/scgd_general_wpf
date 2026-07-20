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
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetDeviceOnline", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetDeviceOnline(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetTakeImageMode", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetTakeImageMode(IntPtr handle, TakeImageMode eTakeImageMode);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Open",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_Open(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LiveOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_LiveOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetGain", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetGain(IntPtr handle, float fGain);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetExpTime",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetExpTime(IntPtr handle, float expTime);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetExpTimeEx",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetExpTimeEx(IntPtr handle, int index, float expTime);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParam", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParam(IntPtr handle, int pid, double val);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsFeatureAvailable",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsFeatureAvailable_Gen(IntPtr handle, int pid);
        public static bool CM_IsFeatureAvailable(IntPtr handle, int pid) => CM_IsFeatureAvailable_Gen(handle, pid);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sleep", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_Sleep(float ms);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetPort",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetPort(IntPtr handle, int port);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsBurstmodeAvailable",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsBurstmodeAvailable(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame_TIFF",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool C_CM_GetFrame_TIFF(IntPtr handle, StringBuilder jsonPm);        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int CM_GetAllCameraIDMD5(StringBuilder jsonCfg, int strLen);



        public static void CM_GetFrame_TIFF(IntPtr handle, string json)
        {
            Thread thread = new Thread(() => workTiffThread(handle, json));
            thread.Start();
        }
        public static void CM_GetFrameEx_TIFF(IntPtr handle, string json)
        {
            Thread thread = new Thread(() => workTiffThreadEx(handle, json));
            thread.Start();
        }

        private static void workTiffThread(IntPtr handle, Object json1)
        {
            StringBuilder json = new StringBuilder();
            json.Append(json1);
            string tifjson = json.ToString();
            C_CM_GetFrame_TIFF(handle, json);
            event_ShowTiff?.Invoke(json.ToString(), false);
        }

        private static void workTiffThreadEx(IntPtr handle, Object json1)
        {
            StringBuilder json = new StringBuilder();
            json.Append(json1);
            string tifjson = json.ToString();
            C_CM_GetFrame_TIFF(handle, json);
            event_ShowTiff?.Invoke(json.ToString(), true);
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameEx_TIFF", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetFrameEx_TIFF(IntPtr handle, StringBuilder jsonPm);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetLiveFrame",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetLiveFrame(IntPtr handle, ref uint w, ref uint h, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetFrame(IntPtr handle, string jsonPm, ref uint w, ref uint h, ref uint srcbpp, ref uint bpp, ref uint channels, byte[] srcrawArray, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetFrame(IntPtr handle, string jsonPm, ref uint w, ref uint h, ref uint srcbpp, ref uint bpp, ref uint channels, IntPtr srcrawBuffer, IntPtr cieBuffer);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ExportToTIFF", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ExportToTIFF(string fileName, uint w, uint h, uint bpp, uint channels, byte[] rawArray);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ExportToTIFFEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ExportToTIFFEx(string fileName, uint w, uint h, uint bpp, uint channels, byte[] rawArray, double dRate);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetDeviceMode",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void C_CM_GetDeviceMode(IntPtr handle, StringBuilder mode, int len);
        public static string CM_GetDeviceMode(IntPtr handle)
        {
            StringBuilder builder = new StringBuilder(1024);
            C_CM_GetDeviceMode(handle, builder, 1024);
            return builder.ToString();
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "LedCheckYaQi", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double LedCheckYaQi(bool isdebug, int checkChannel, UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] imgdata
            , int isguding, int gudingrid, int lunkuomianji, int pointNum, double hegexishu, int erzhihuapiancha, double[] databanjin
            , int[] datazuobiaoX, int[] datazuobiaoY, int picwid
            , int pichig, int[] 关注范围, int 发光区二值化补正, int boundry, double[] LengthCheck, double[] LengthRange, double[] LengthResult, bool isuseLocalRdPoint, float[] localRdMark, double[] PointX, double[] PointY);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSN",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void CM_GetSN(IntPtr handle, StringBuilder sn, int len);

        public static string CM_GetSN(IntPtr handle)
        {
            StringBuilder builder = new StringBuilder(50);
            CM_GetSN(handle, builder, 50);
            return builder.ToString();
        }
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SplitData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SplitData(uint w, uint h, uint bpp, uint channels, byte[] imgdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SplitDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SplitDataEx(int w, int h, int bpp, byte[] srcimgdata, byte[] dstX, byte[] dstY, byte[] dstZ);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvCircle",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetXYZxyuvCircle(IntPtr handle, uint pX, uint pY, ref float X, ref float Y, ref float Z, ref float x, ref float y, ref float u, ref float v, double nRadius = 3);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetXYZxyuvEx(uint[] pX, uint[] pY, float[] X, float[] Y, float[] Z, float[] x, float[] y, float[] u, float[] v, uint nLen, string fileName, double nRadius = 0.0);

        public struct ChromaInfo
        {
            public float fX { set; get; }
            public float fY { set; get; }
            public float fZ { set; get; }
            public float fx { set; get; }
            public float fy { set; get; }
            public float fu { set; get; }
            public float fv { set; get; }
            public float fCCT { set; get; }
            public float fWave { set; get; }
            public int nMidPointX { set; get; }
            public int nMidPointY { set; get; }
        };

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetMask", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetMask(IntPtr handle, int[] pX, int[] pY, uint nLen, ref ChromaInfo chromaInfo);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_MergeData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_MergeData(uint w, uint h, uint bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_MergeDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_MergeDataEx(uint w, uint h, uint bpp, byte[] imgdata, byte[] srcX, byte[] srcY, byte[] srcZ);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetBGRBuffer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetBGRBuffer(IntPtr handle, ref int w, ref int h, ref int bpp, ref int channels, byte[] imgdata);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern uint CM_GetFrameMemLength(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameMaxMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern ulong CM_GetFrameMaxMemLength(IntPtr handle);
    }
}
