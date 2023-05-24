using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public class PG
    {
        private const string LibraryCVCamera = "cvCamera.dll";

        [DllImport(LibraryCVCamera, EntryPoint = "CM_InitPG",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe IntPtr CM_InitPG(PG_Type ePgType, Communicate_Type eCOMType);

        [DllImport(LibraryCVCamera, EntryPoint = "CM_UnInitPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_UnInitPG(IntPtr handle);

        [DllImport(LibraryCVCamera, EntryPoint = "CM_ConnectToPG", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_ConnectToPG(IntPtr handle, string szIPAddress, uint nPort);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_InitSerialPG", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_InitSerialPG(IntPtr handle, string szComName, ulong BaudRate);
        
        [DllImport(LibraryCVCamera, EntryPoint = "CM_IsConnectedPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_IsConnectedPG(IntPtr handle);

        [DllImport(LibraryCVCamera, EntryPoint = "CM_StartPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_StartPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_StopPG",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_StopPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_ReSetPG",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_ReSetPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_SwitchUpPG",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_SwitchUpPG(IntPtr handle);

        [DllImport(LibraryCVCamera, EntryPoint = "CM_SwitchDownPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_SwitchDownPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_SwitchFramePG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe bool CM_SwitchFramePG(IntPtr handle, int nIndex);


        ///StartPG
        public static bool CMStartPG(IntPtr handle) => CM_StartPG(handle);
        //初始化PG句柄
        public static IntPtr CMInitPG(PG_Type ePgType, Communicate_Type eCOMType) => CM_InitPG(ePgType, eCOMType);
        //释放PG句柄
        public static bool CMUnInitPG(IntPtr handle) => CM_UnInitPG(handle);
        //连接至PG(TCP/IP)
        public static bool CMConnectToPG(IntPtr handle, string szIPAddress, uint nPort) => CM_ConnectToPG(handle, szIPAddress, nPort);
        //连接至PG(COM口)
        public static bool CMInitSerialPG(IntPtr handle, string szComName, ulong BaudRate) => CM_InitSerialPG(handle, szComName, BaudRate);
        //判断PG是否已连接
        public static bool CMIsConnectedPG(IntPtr handle) => CM_IsConnectedPG(handle);
        //StopPG
        public static bool CMStopPG(IntPtr handle) => CM_StopPG(handle);
        //ReSetPG
        public static bool CMReSetPG(IntPtr handle) => CM_ReSetPG(handle);
        //CM_SwitchUpPG
        public static bool CMSwitchUpPG(IntPtr handle) => CM_SwitchUpPG(handle);
        //CM_SwitchDownPG
        public static bool CMSwitchDownPG(IntPtr handle) => CM_SwitchDownPG(handle);
        //CM_SwitchFramePG
        public static bool CMSwitchFramePG(IntPtr handle, int nIndex) => CM_SwitchFramePG(handle, nIndex);

    }
}