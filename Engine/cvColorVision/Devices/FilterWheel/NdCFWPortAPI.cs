#pragma warning disable
using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{

    /// <summary>
    /// NdCFWPort 滤光片轮端口相关操作
    /// </summary>
    public static class NdCFWPortAPI
    {
        // 假设 dll 名称同样为 cvCamera.dll，如果是其它的请自行替换
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        // 如果与 Spectrometer.cs 里的定义冲突，也可以直接复用 Spectrometer.AutoTime_CallBack
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int AutoTime_CallBack(int time, double spectum);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatNdCFWPort", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CM_CreatNdCFWPort(string szComName, uint BaudRate, bool bCvCam = false, uint dwTimeOut = 30000);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseNdCFWPort", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_ReleaseNdCFWPort(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_BoundCamera", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_BoundCamera(IntPtr handle, IntPtr hCamera);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetNdCfwPortList", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetNdCfwPortList(IntPtr handle, int[] nNdRate, int nSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetNdCFWPort", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetNdCFWPort(IntPtr handle, int nPort);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetNdCFWPortEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetNdCFWPortEx(IntPtr handle, int nPort);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetNdCFWPortOutWait", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetNdCFWPortOutWait(IntPtr handle, int nPort);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetNdCFWPort", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetNdCFWPort(IntPtr handle, ref int nPort);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraAutoExpTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetCameraAutoExpTime(IntPtr handle, ref int nNewPort, ref int nOldPort, ref float dExpTime);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetNdMaxMinExpTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetNdMaxMinExpTime(IntPtr handle, double dMaxExp, double dMinExp);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_BoundEmission", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_BoundEmission(IntPtr handle, IntPtr hEmission);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetEmissionAutoTimeEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetEmissionAutoTimeEx(IntPtr handle, int iLimitTime, float fTimeB, float dMaxva1, AutoTime_CallBack CallBackFunc);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetEmissionAutoExpTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetEmissionAutoExpTime(IntPtr handle, ref int nNewPort, ref int nOldPort, ref float dExpTime);

        // 以下两个接口是根据 cpp 文件最后两行导出的函数额外补充的（.h 中未提供）
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_AddNdItemFile", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_AddNdItemFile(IntPtr handle, int nIndex, CalibrationType eCaliType, string filename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_DeleteNdAllItems", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_DeleteNdAllItems(IntPtr handle, int nIndex);
    }
}