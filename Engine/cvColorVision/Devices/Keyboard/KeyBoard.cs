#pragma warning disable

using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public class KeyBoardDLL
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CM_InitialKeyBoardSrc(int w, int h, int bpp, int channels, IntPtr imgdata, int saveProcessData, string debugPath ,float exp, string luminFile,int doCali =1);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern float CM_CalculateHalo(IRECT keyRect, int outMOVE, int threadV, int haloSize, string savePath,  ushort[] gray, ref uint pixNum);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern float CM_CalculateKey(IRECT keyRect, int inMOVE, int threadV, string path, ushort[] gray,ref uint pixNum);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_GetKeyBoardResult(ref int w, ref int h, ref int bpp, ref int channels, byte[] pData);
    }

}
