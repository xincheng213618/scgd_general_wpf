using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    // 与 C++ 对应的枚举
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Pss_Type
    {
        Keithley_2400 = 0,
        Keithley_2600 = 1,
        Precise_S100 = 2,
        Vxi11Protocol = 3,
        VictualPss = 4
    }

    public class PassSx
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";
        public const int SuccessCode = 1;
        private static bool _isSourceV = true;

        public static bool IsSuccess(int resultCode) => resultCode == SuccessCode;

        public static string GetErrorMessage(int resultCode) => Spectrometer.GetErrorMessage(resultCode);

        public static string FormatErrorMessage(string operation, int resultCode)
        {
            string errorMessage = GetErrorMessage(resultCode);
            return string.IsNullOrWhiteSpace(errorMessage)
                ? $"{operation} 失败，错误码: {resultCode}"
                : $"{operation} 失败: {errorMessage}";
        }

        // 打开
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_OpenNetDevice", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_OpenNetDeviceRaw([MarshalAs(UnmanagedType.Bool)] bool bNet, string devName, Pss_Type nType);

        public static int OpenNetDevice(bool bNet, string devName, Pss_Type nType) => cvPss_Sx_OpenNetDeviceRaw(bNet, devName, nType);

        // 关闭
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_CloseDevice", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPssSxCloseDeviceRaw(int nDevID);
        public static int CloseDeviceCode(int nDevID) => cvPssSxCloseDeviceRaw(nDevID);
        public static bool CloseDevice(int nDevID) => IsSuccess(CloseDeviceCode(nDevID));

        // 获取ID和长度
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_GetIDN", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_GetIDN_ByteArray(int nDevID, [Out] byte[] pszIdn, ref int strLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_GetIDN", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_GetIDN_StringBuilder(int nDevID, StringBuilder pszIdn, ref int strLen);

        public static int cvPssSxGetIDNCode(int nDevID, byte[] pszIdn, ref int strLen) => cvPss_Sx_GetIDN_ByteArray(nDevID, pszIdn, ref strLen);
        public static int cvPssSxGetIDNCode(int nDevID, StringBuilder pszIdn, ref int strLen) => cvPss_Sx_GetIDN_StringBuilder(nDevID, pszIdn, ref strLen);
        public static bool cvPssSxGetIDN(int nDevID, byte[] pszIdn, ref int strLen) => IsSuccess(cvPssSxGetIDNCode(nDevID, pszIdn, ref strLen));
        public static bool cvPssSxGetIDN(int nDevID, StringBuilder pszIdn, ref int strLen) => IsSuccess(cvPssSxGetIDNCode(nDevID, pszIdn, ref strLen));

        // 以恒压/恒流模式为准设置（设置后缓存标志位供其它方法转换单位）
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetSourceV", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPssSxSetSourceVRaw(int nDevID, [MarshalAs(UnmanagedType.I1)] bool isSourceV);
        public static int cvPssSxSetSourceVCode(int nDevID, bool isSourceV)
        {
            int resultCode = cvPssSxSetSourceVRaw(nDevID, isSourceV);
            if (IsSuccess(resultCode))
            {
                _isSourceV = isSourceV;
            }
            return resultCode;
        }
        public static bool cvPssSxSetSourceV(int nDevID, bool isSourceV)
        {
            return IsSuccess(cvPssSxSetSourceVCode(nDevID, isSourceV));
        }

        // 源表硬件设置（4线制/2线制，前后接口）
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_Set4WireFront", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_Set4WireFrontRaw(int nDevID, [MarshalAs(UnmanagedType.I1)] bool bWier, [MarshalAs(UnmanagedType.I1)] bool bFront);
        public static int Set4WireFrontCode(int nDevID, bool bWier, bool bFront) => cvPss_Sx_Set4WireFrontRaw(nDevID, bWier, bFront);
        public static bool Set4WireFront(int nDevID, bool bWier, bool bFront) => IsSuccess(Set4WireFrontCode(nDevID, bWier, bFront));

        // 设置通道 A 还是 B
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetSrcAorB", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_SetSrcAorBRaw(int nDevID, [MarshalAs(UnmanagedType.I1)] bool bSrcA);
        public static int SetSrcAorBCode(int nDevID, bool bSrcA) => cvPss_Sx_SetSrcAorBRaw(nDevID, bSrcA);
        public static bool SetSrcAorB(int nDevID, bool bSrcA) => IsSuccess(SetSrcAorBCode(nDevID, bSrcA));

        // 采集循环里的延迟时间
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetDelayTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_SetDelayTimeRaw(int nDevID, double dTimems);
        public static int SetDelayTimeCode(int nDevID, double dTimems) => cvPss_Sx_SetDelayTimeRaw(nDevID, dTimems);
        public static bool SetDelayTime(int nDevID, double dTimems) => IsSuccess(SetDelayTimeCode(nDevID, dTimems));

        // 关闭源输出
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_CloseOutput", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_CloseOutputRaw(int nDevID);
        public static int CvPssSxCloseOutputCode(int nDevID) => cvPss_Sx_CloseOutputRaw(nDevID);
        public static bool CvPssSxCloseOutput(int nDevID) => IsSuccess(CvPssSxCloseOutputCode(nDevID));

        // 设置电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvMeasureDataRaw(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);

        public static int cvMeasureDataCode(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            if (_isSourceV) return cvMeasureDataRaw(nDevID, measureVal, lmtVal / 1000.0, ref rstV, ref rstI);
            return cvMeasureDataRaw(nDevID, measureVal / 1000.0, lmtVal, ref rstV, ref rstI);
        }

        public static bool cvMeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            return IsSuccess(cvMeasureDataCode(nDevID, measureVal, lmtVal, ref rstV, ref rstI));
        }

        // 获取结果
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetMeasureResult", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvGetMeasureResultRaw(int nDevID, ref double rstV, ref double rstI);
        public static int cvGetMeasureResultCode(int nDevID, ref double rstV, ref double rstI) => cvGetMeasureResultRaw(nDevID, ref rstV, ref rstI);
        public static bool cvGetMeasureResult(int nDevID, ref double rstV, ref double rstI) => IsSuccess(cvGetMeasureResultCode(nDevID, ref rstV, ref rstI));

        // 设置单步电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvStepMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvStepMeasureDataRaw(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);
        public static int cvStepMeasureDataCode(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            if (_isSourceV) return cvStepMeasureDataRaw(nDevID, measureVal, lmtVal / 1000.0, ref rstV, ref rstI);
            return cvStepMeasureDataRaw(nDevID, measureVal / 1000.0, lmtVal, ref rstV, ref rstI);
        }
        public static bool cvStepMeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            return IsSuccess(cvStepMeasureDataCode(nDevID, measureVal, lmtVal, ref rstV, ref rstI));
        }

        // 设置单步量程参数并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvStepMeasureDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvStepMeasureDataExRaw(int nDevID, double srcRng, double lmtRng, double measureVal, double lmtVal, ref double rstV, ref double rstI);
        public static int cvStepMeasureDataExCode(int nDevID, double srcRng, double lmtRng, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            if (_isSourceV) return cvStepMeasureDataExRaw(nDevID, srcRng, lmtRng / 1000.0, measureVal, lmtVal / 1000.0, ref rstV, ref rstI);
            return cvStepMeasureDataExRaw(nDevID, srcRng / 1000.0, lmtRng, measureVal / 1000.0, lmtVal, ref rstV, ref rstI);
        }
        public static bool cvStepMeasureDataEx(int nDevID, double srcRng, double lmtRng, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            return IsSuccess(cvStepMeasureDataExCode(nDevID, srcRng, lmtRng, measureVal, lmtVal, ref rstV, ref rstI));
        }

        // 序列扫描 指定量程
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvSweepData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvSweepDataRaw(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, [Out] double[] pVList, [Out] double[] pIList);
        public static int cvSweepDataCode(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList)
        {
            if (_isSourceV) return cvSweepDataRaw(nDevID, srcRng, lmtRng / 1000.0, lmtVal / 1000.0, startVal, stopVal, points, pVList, pIList);
            return cvSweepDataRaw(nDevID, srcRng / 1000.0, lmtRng, lmtVal, startVal / 1000.0, stopVal / 1000.0, points, pVList, pIList);
        }
        public static bool cvSweepData(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList)
        {
            return IsSuccess(cvSweepDataCode(nDevID, srcRng, lmtRng, lmtVal, startVal, stopVal, points, pVList, pIList));
        }

        // 序列扫描 自动量程
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvSweepDataAutoRng", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvSweepDataAutoRngRaw(int nDevID, double lmtVal, double startVal, double stopVal, int points, [Out] double[] pVList, [Out] double[] pIList);
        public static int cvSweepDataAutoRngCode(int nDevID, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList)
        {
            if (_isSourceV) return cvSweepDataAutoRngRaw(nDevID, lmtVal / 1000.0, startVal, stopVal, points, pVList, pIList);
            return cvSweepDataAutoRngRaw(nDevID, lmtVal, startVal / 1000.0, stopVal / 1000.0, points, pVList, pIList);
        }
        public static bool cvSweepDataAutoRng(int nDevID, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList)
        {
            return IsSuccess(cvSweepDataAutoRngCode(nDevID, lmtVal, startVal, stopVal, points, pVList, pIList));
        }

        // 序列扫描 自定义值列表
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvListData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvListDataRaw(int nDevID, double srcRng, double lmtRng, double lmtVal, [In] double[] pCustomData, int points, [Out] double[] pVList, [Out] double[] pIList);
        public static int cvListDataCode(int nDevID, double srcRng, double lmtRng, double lmtVal, double[] pCustomData, int points, double[] pVList, double[] pIList)
        {
            if (_isSourceV) return cvListDataRaw(nDevID, srcRng, lmtRng / 1000.0, lmtVal / 1000.0, pCustomData, points, pVList, pIList);
            return cvListDataRaw(nDevID, srcRng / 1000.0, lmtRng, lmtVal, pCustomData, points, pVList, pIList);
        }
        public static bool cvListData(int nDevID, double srcRng, double lmtRng, double lmtVal, double[] pCustomData, int points, double[] pVList, double[] pIList)
        {
            return IsSuccess(cvListDataCode(nDevID, srcRng, lmtRng, lmtVal, pCustomData, points, pVList, pIList));
        }
    }
}