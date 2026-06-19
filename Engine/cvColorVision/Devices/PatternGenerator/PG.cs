#pragma warning disable
using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public enum PG_Type
    {
        GX09C_LCM = 0,
        SKYCODE,
    };


    public class PG
    {



        private const string LibraryCVCamera = "cvCamera.dll";



        [DllImport(LibraryCVCamera, EntryPoint = "CM_InitPG",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe IntPtr CM_InitPG(PG_Type ePgType, Communicate_Type eCOMType);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_UnInitPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_UnInitPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_ConnectToPG", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_ConnectToPG(IntPtr handle, string szIPAddress, uint nPort);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_InitSerialPG", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_InitSerialPG(IntPtr handle, string szComName, ulong BaudRate);
        
        [DllImport(LibraryCVCamera, EntryPoint = "CM_ClosePG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_ClosePG(IntPtr handle);

        [DllImport(LibraryCVCamera, EntryPoint = "CM_IsConnectedPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_IsConnectedPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_StartPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_StartPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_StopPG",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_StopPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_ReSetPG",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_ReSetPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_SwitchUpPG",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_SwitchUpPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_SwitchDownPG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_SwitchDownPG(IntPtr handle);
        [DllImport(LibraryCVCamera, EntryPoint = "CM_SwitchFramePG", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool CM_SwitchFramePG(IntPtr handle, int nIndex);

    }
}