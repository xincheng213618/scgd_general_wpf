using System;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    /// <summary>
    /// 光谱仪相关操作
    /// </summary>
    public static class Spectrometer
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        // EXPORTC MYDLL BOOL STDCALL CM_GetErrorMessage(int nErr, char* szMsg, int& strLen);
        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int CM_GetErrorMessage( int nErr, StringBuilder szMsg, ref int strLen);

        /// <summary>
        /// 根据错误码获取对应的错误描述信息
        /// </summary>
        /// <param name="errorCode">CM_ 系列函数返回的错误码</param>
        /// <returns>可读的错误信息；成功(1)时返回空字符串</returns>
        public static string GetErrorMessage(int errorCode)
        {
            if (errorCode == 1) return string.Empty;
            try
            {
                const int bufferSize = 1024;
                StringBuilder sb = new StringBuilder(bufferSize);
                int len = bufferSize;
                CM_GetErrorMessage(errorCode, sb, ref len);
                string msg = sb.ToString();
                return string.IsNullOrEmpty(msg) ? $"未知错误 (错误码: {errorCode})" : $"{errorCode} ErrorMessage:{msg}" ;
            }
            catch
            {
                return $"未知错误 (错误码: {errorCode})";
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int Emission_CallBack(IntPtr strText, int nLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateEmission", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CM_CreateEmission(int nType, Emission_CallBack lpCallBack);

        public static IntPtr CM_CreateEmission(SpectrometerType type, Emission_CallBack lpCallBack) => CM_CreateEmission((int)type, lpCallBack);

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

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_LoadMagiudeFileWithND", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_LoadMagiudeFileWithND(IntPtr handle, int nIndex, string szFileName);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetSrcData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetSrcData(IntPtr handle, float fIntTime, int iAveNum, double[] pdSpectumData, ref int pSpectumNumber);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetSerialNumber", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetSpectrSerialNumber(IntPtr handle, StringBuilder szSerialNum);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetAllSN", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetAllSN(int nType, int nComPort, StringBuilder sn, int len);

        public static int CM_Emission_GetAllSN(SpectrometerType type, int nComPort, StringBuilder sn, int len) => CM_Emission_GetAllSN((int)type, nComPort, sn, len);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_AutoDarkStorage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_AutoDarkStorage(IntPtr handle, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_DarkStorage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_DarkStorage(IntPtr handle, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_DarkStorageEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_DarkStorageEx(IntPtr handle, int nPort, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetData(IntPtr handle, TRIGGER_MODE TriggerMode, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, float fSetWL1, float fSetWL2, ref COLOR_PARA dPara);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetDataWithND", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetDataWithND(IntPtr handle, int nND, TRIGGER_MODE TriggerMode, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, float fSetWL1, float fSetWL2, ref COLOR_PARA dPara);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetDataEQE", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetDataEQE(IntPtr handle, TRIGGER_MODE TriggerMode, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, double divisor, float dVoltage, float dCurrent, ref COLOR_PARA_EQE dParaEqe);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetDataEQEWithND", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetDataEQEWithND(IntPtr handle, int nND, TRIGGER_MODE TriggerMode, float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, double divisor, float dVoltage, float dCurrent, ref COLOR_PARA_EQE dParaEqe);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetDataSyncfreq", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetDataSyncfreq(IntPtr handle, TRIGGER_MODE TriggerMode, double Syncfreq, int m, ref float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, float fSetWL1, float fSetWL2, ref COLOR_PARA dPara);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetDataSyncfreqWithND", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetDataSyncfreqWithND(IntPtr handle, int nND, TRIGGER_MODE TriggerMode, double Syncfreq, int m, ref float fIntTime, int iAveNum, int iFilterBW, float[] fDarkData, float fDx, float fDy, float fSetWL1, float fSetWL2, ref COLOR_PARA dPara);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int AutoTime_CallBack(int time, double spectum);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetAutoTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetAutoTime(IntPtr handle, ref float fIntTime, int iLimitTime, float fTimeB, int nSaturation);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_GetAutoTimeEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_GetAutoTimeEx(IntPtr handle, ref float fIntTime, int iLimitTime, float fTimeB, float dMaxva1, AutoTime_CallBack autoTime_CallBack);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_BoundNdCFWPort", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_BoundNdCFWPort(IntPtr handle, IntPtr hNdCFW);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_Init_Auto_Dark", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_Init_Auto_Dark(IntPtr handle, float fTimeStart, int nStepTime, int nStepCount, int iAveNum);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_Init_Auto_DarkEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_Init_Auto_DarkEx(IntPtr handle, int nPort, float fTimeStart, int nStepTime, int nStepCount, int iAveNum);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Emission_CreateMagiude", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_Emission_CreateMagiude(float fIntTime, float[] fDarkData, float[] fLightData, string szCSFile, string szWavaLengthFile, string szMagiude);
    }
}
