using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{

    public static class ConvertXYZ
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        #region cvConvertXYZ.cpp

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_InitXYZ(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UnInitXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_UnInitXYZ(IntPtr handle);  

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetBufferXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetBufferXYZ(IntPtr handle, UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseBuffer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_ReleaseBuffer(IntPtr handle);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetFilter", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetFilter(IntPtr handle, bool bEnable, float fthreshold);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetFilterNoArea", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetFilterNoArea(IntPtr handle, bool bEnable, float fthreshold);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetFilterXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetFilterXYZ(IntPtr handle, bool bEnable,int nType, float fthreshold);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvCircle(IntPtr handle, int pX, int pY, ref float X, ref float Y, ref float Z, ref float x, ref float y, ref float u, ref float v, double nRadius = 3);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvRect(IntPtr handle, int pX, int pY, ref float X, ref float Y, ref float Z, ref float x, ref float y, ref float u, ref float v, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYRect(IntPtr handle, int pX, int pY, ref float Y, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZCircle(IntPtr handle, int nX, int nY, ref float dX, ref float dY, ref float dZ, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyCircle(IntPtr handle, int nX, int nY, ref float dx, ref float dy, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdx, float[] pdy, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvCircle(IntPtr handle, int nX, int nY, ref float du, ref float dv, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdu, float[] pdv, int nLen, string szFileName, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, float[] pdx, float[] pdy, float[] pdu, float[] pdv, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYCircle(IntPtr handle, int nX, int nY, ref float dY, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdY, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZRect(IntPtr handle, int nX, int nY, ref float dX, ref float dY, ref float dZ, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, int nLen, string szFileName, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyRect(IntPtr handle, int nX, int nY, ref float dx, ref float dy, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdx, float[] pdy, int nLen, string szFileName, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvRect(IntPtr handle, int nX, int nY, ref float du, ref float dv, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdu, float[] pdv, int nLen, string szFileName, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, float[] pdx, float[] pdy, float[] pdu, float[] pdv, int nLen, string szFileName, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdY, int nLen, string szFileName, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetxyuvCCTWaveCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetxyuvCCTWaveCircle(IntPtr handle, int pX, int pY, ref float x, ref float y, ref float u, ref float v, ref float CCT, ref float Wave, double nRadius = 3);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetxyuvCCTWave", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetxyuvCCTWave(IntPtr handle, int pX, int pY, ref float x, ref float y, ref float u, ref float v, ref float CCT, ref float Wave);
        #endregion
    }


    public partial class cvCameraCSLib
    {
        #region cvConvertXYZ.cpp

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_InitXYZ(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UnInitXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_UnInitXYZ(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetBufferXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetBufferXYZ(IntPtr handle, UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseBuffer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_ReleaseBuffer(IntPtr handle);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetFilter", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetFilter(IntPtr handle, bool bEnable, float fthreshold);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetFilterNoArea", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetFilterNoArea(IntPtr handle, bool bEnable, float fthreshold);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetFilterXYZ", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetFilterXYZ(IntPtr handle, bool bEnable, int nType, float fthreshold);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvCircle(IntPtr handle, int pX, int pY, ref float X, ref float Y, ref float Z, ref float x, ref float y, ref float u, ref float v, double nRadius = 3);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvRect(IntPtr handle, int pX, int pY, ref float X, ref float Y, ref float Z, ref float x, ref float y, ref float u, ref float v, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYRect(IntPtr handle, int pX, int pY, ref float Y, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZCircle(IntPtr handle, int nX, int nY, ref float dX, ref float dY, ref float dZ, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyCircle(IntPtr handle, int nX, int nY, ref float dx, ref float dy, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdx, float[] pdy, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvCircle(IntPtr handle, int nX, int nY, ref float du, ref float dv, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdu, float[] pdv, int nLen, string szFileName, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, float[] pdx, float[] pdy, float[] pdu, float[] pdv, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYCircle(IntPtr handle, int nX, int nY, ref float dY, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdY, int nLen, string szFileName, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZRect(IntPtr handle, int nX, int nY, ref float dX, ref float dY, ref float dZ, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, int nLen, string szFileName, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyRect(IntPtr handle, int nX, int nY, ref float dx, ref float dy, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeToxyRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeToxyRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdx, float[] pdy, int nLen, string szFileName, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvRect(IntPtr handle, int nX, int nY, ref float du, ref float dv, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZChangeuvRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZChangeuvRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdu, float[] pdv, int nLen, string szFileName, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetXYZxyuvRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY, float[] pdZ, float[] pdx, float[] pdy, float[] pdu, float[] pdv, int nLen, string szFileName, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetYRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdY, int nLen, string szFileName, int nRw, int nRh);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetxyuvCCTWaveCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetxyuvCCTWaveCircle(IntPtr handle, int pX, int pY, ref float x, ref float y, ref float u, ref float v, ref float CCT, ref float Wave, double nRadius = 3);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetxyuvCCTWave", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetxyuvCCTWave(IntPtr handle, int pX, int pY, ref float x, ref float y, ref float u, ref float v, ref float CCT, ref float Wave);
        #endregion
    }
}
