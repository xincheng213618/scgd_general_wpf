#pragma warning disable  CA1051,CA1707,CA1711,CA1401,CA2101
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct COLOR_PARA
    {
        public float fCIEx;        //色坐标
        public float fCIEy;
        public float fCIEz;

        public float fx;        //色度x
        public float fy;        //色度y
        public float fu;
        public float fv;

        public float fCCT;      //相关色温(K)
        public float dC;        //色差dC
        public float fLd;       //主波长(nm)
        public float fPur;      //色纯度(%)
        public float fLp;       //峰值波长(nm)
        public float fHW;       //半波宽(nm)
        public float fLav;      //平均波长(nm)
        public float fRa;       //显色性指数 Ra
        public float fRR;       //红色比
        public float fGR;       //绿色比
        public float fBR;       //蓝色比
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        public float[] fRi;   //显色性指数 R1-R15

        public float fIp;      //峰值AD

        public float fPh;      //光度值
        public float fPhe;     //辐射度值
        public float fPlambda; //绝对光谱洗漱
        public float fSpect1;  //起始波长
        public float fSpect2;  //结束波长
        public float fInterval; //波长间隔

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10000)]
        public float[] fPL;   //光谱数据
    };

    public enum TRIGGER_MODE
    {
        SOFTWARE_SYNCHRONOUS,   //软件同步模式
        SOFTWARE_ASYNCHRONOUS,  //软件异步模式
        SOFTWARE_AUTO,          //自动采样模式
        EXINT_RISING_EDGE,      //上升沿触发
        EXINT_FALLING_EDGE,     //下降沿触发
        EXINT_HIGH_LEVEL,       //高电平触发模式
        EXINT_LOW_LEVEL,        //低电平触发模式
    }
    /// <summary>
    /// 光谱仪相关操作
    /// </summary>
    public static class Spectrometer
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int Emission_CallBack(IntPtr strText, int nLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateEmission", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CM_CreateEmission(int nType, Emission_CallBack lpCallBack);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseEmission", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_ReleaseEmission(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetEmissionSP100(IntPtr handle, bool bEnable, int nStartPos, int nEndPos, double m_dMeanThreshold);

        //连接光谱仪
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_Init", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_Init(IntPtr handle, int nComPort, int dwBaudRate);

        //关闭光谱仪
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_Close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_Close(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_LoadWavaLengthFile", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_LoadWavaLengthFile(IntPtr handle, string szFileName);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_LoadMagiudeFile", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_LoadMagiudeFile(IntPtr handle, string szFileName);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetSrcData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CM_Emission_GetSrcData(IntPtr handle, float fIntTime, int iAveNum, double[] pdSpectumData, ref int pSpectumNumber);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSpectrSerialNumber", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CM_GetSpectrSerialNumber(IntPtr handle, StringBuilder szSerialNum);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_AutoDarkStorage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_AutoDarkStorage(IntPtr handle, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_DarkStorage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_DarkStorage(IntPtr handle, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetData(IntPtr handle, TRIGGER_MODE TriggerMode, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, float fSetWL1, float fSetWL2, ref COLOR_PARA dPara);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetDataSyncfreq", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetDataSyncfreq(IntPtr handle, TRIGGER_MODE TriggerMode, double Syncfreq, int m, ref float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, float fSetWL1, float fSetWL2, ref COLOR_PARA dPara);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int AutoTime_CallBack(int time, double spectum);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetAutoTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetAutoTime(IntPtr handle, ref float fIntTime, int iLimitTime, float fTimeB, int nSaturation);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetAutoTimeEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetAutoTimeEx(IntPtr handle, ref float fIntTime, int iLimitTime, float fTimeB, float dMaxva1, AutoTime_CallBack autoTime_CallBack);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_Init_Auto_Dark", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_Init_Auto_Dark(IntPtr handle, float fTimeStart, int nStepTime, int nStepCount, int iAveNum);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_CreateMagiude", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CM_Emission_CreateMagiude(float fIntTime, float[] fDarkData, float[] fLightData, string szCSFile, string szWavaLengthFile, string szMagiude);
    }
}


