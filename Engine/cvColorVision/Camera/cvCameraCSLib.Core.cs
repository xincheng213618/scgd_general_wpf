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
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";



        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCfgToJson",
    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void C_GetCfgToJson(IntPtr handle, ConfigType eType, StringBuilder jsonCfg, int len, bool bDefault);

        public static string GetCfgToJson(IntPtr handle, ConfigType eType, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            C_GetCfgToJson(handle, eType, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }


        public static TiffShowEvent event_ShowTiff;
        public static LiveShowEvent event_ShowLive;

        public static byte[] liveImageDataPartShow;
        public static int picw;
        public static int pich;
        public static int picbpp;
        public static int picchannels;


        private static string UnicodeToGB(string text) => Encoding.GetEncoding("gb2312").GetString(Encoding.Convert(Encoding.Unicode, Encoding.GetEncoding("gb2312"), Encoding.Unicode.GetBytes(text)));
        public delegate ulong QHYCCDProcCallBack(int handle, IntPtr pData, int nW, int nH, int lss, int bpp, int channels, IntPtr usrData);

        public static int connectedCameraType = 1;
    }
}
