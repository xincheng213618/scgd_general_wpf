#pragma warning disable
using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public enum Communicate_Type
    {
        Communicate_Tcp = 0,
        Communicate_Serial,
    };

    public class SensorComm
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";


        //初始化传感器
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitSensorComm",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_InitSensorComm(Communicate_Type eCOM_Type);

        //释放传感器
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UnInitSensorComm", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_UnInitSensorComm(IntPtr handle);

        //连接至传感器(TCP/IP)
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ConnectToSensor",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ConnectToSensor(IntPtr handle, string szIPAddress, uint nPort);

        //连接至传感器(COM口)
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitSerialSensor", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_InitSerialSensor(IntPtr handle, string szComName, ulong BaudRate);

        //判断传感器是否已连接
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SensorConnected", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SensorConnected(IntPtr handle);

        //断开传感器的连接
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_DestroySensor", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_DestroySensor(IntPtr handle);

        //发送TP指令
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SendCmdToSensor", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SendCmdToSensor(IntPtr handle, string szPassWord, string szStart, ref bool bRet);

        //发送传感器指令
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SendCmdToSensorEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SendCmdToSensorEx(IntPtr handle, string szCmd, byte[] szResponses, ulong dwTimeOut);

        //发送传感器指令2
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SendCmdToSensorEx2", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SendCmdToSensorEx2(IntPtr handle, byte[] szCmd, int nSendLen, byte[] szResponses, int nResLen, ulong dwTimeOut);

        //判断TP指令的执行结果
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_FindFileNOK", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_FindFileNOK(string szFilePath, ref bool bRet);

        //创建TCP服务
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateTCPServer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_CreateTCPServer(uint nPort);


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReceiveCallback(IntPtr data1, IntPtr data2, byte[] data3, int data4);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ListenCallback(IntPtr data1, IntPtr data2, bool data3);

        //创建TCP服务
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateTCPServer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_CreateTCPServer(uint nPort, IntPtr hOperate);

        //创建TCP服务
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateTCPServer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_CreateTCPServer(uint nPort, IntPtr hOperate, ReceiveCallback callBack);

        //创建TCP服务
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateTCPServer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_CreateTCPServer(uint nPort, IntPtr hOperate, ReceiveCallback callBack, ListenCallback callback2);

        //判断服务端的Socket是否已经打开
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_IsSocketOpened",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_Sv_IsSocketOpened(IntPtr hServer);

        //判断指定的客户端是否已经连接服务端
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_IsConnected",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_Sv_IsConnected(IntPtr hServer, IntPtr hClient);

        //向所有的客服端发送数据
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_Send", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern ulong CM_Sv_Send(IntPtr hServer, byte[] lpBuffer, ulong dwLength);

        //向单个客服端发送数据
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_SendEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern ulong CM_Sv_SendEx(IntPtr hServer, IntPtr hClient, byte[] lpBuffer, ulong dwLength);

        //获取客户端IP
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_GetClientIP", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_Sv_GetClientIP(IntPtr hServer, IntPtr hClient, char[] szIPAddress, ref uint nPort);

        //获取客户端的数量
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_GetClientCount", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern long CM_Sv_GetClientCount(IntPtr hServer);

        //获取客户端的数量
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_GetClientHandle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern long CM_Sv_GetClientHandle(IntPtr hServer, IntPtr[] phClient);

        //获取客户端的数量
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sv_GetClientHandleEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_Sv_GetClientHandleEx(IntPtr hServer, uint nIndex);
    }
}


