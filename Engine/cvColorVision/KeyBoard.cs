#pragma warning disable CA2101,CA1401,CA1707,SYSLIB
using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public class KeyBoardDLL
    {
        private const string LIBRARY_CVCAMERA = "libs\\cvCamera.dll";

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CM_InitialKeyBoardSrc(int w, int h, int bpp, int channels, IntPtr imgdata, int saveProcessData, string debugPath ,float exp, string luminFile,int doCali =1);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern float CM_CalculateHalo(IRECT keyRect, int outMOVE, int threadV, int haloSize, string savePath, ref short gray, ref uint pixNum);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern float CM_CalculateKey(IRECT keyRect, int inMOVE, int threadV, string path,ref short gray,ref uint pixNum);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_GetKeyBoardResult(ref int w, ref int h, ref int bpp, ref int channels, byte[] pData);
    }

}
