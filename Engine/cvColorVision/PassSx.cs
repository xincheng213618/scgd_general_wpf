using System.Runtime.InteropServices;

namespace cvColorVision
{
    // 与 C++ 对应的枚举
    public enum Pss_Type
    {
        kethiley2400 = 0,
        kethiley2600 = 1,
        PssSx00Dll = 2,
        Vxi11Protocol = 3,
        VictualPss = 4,      // 虚拟源
    }

    public class PassSx
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";
        private static bool _isSourceV = true;

        // 打开
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_OpenNetDevice", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int cvPss_Sx_OpenNetDeviceRaw(bool bNet, string devName, Pss_Type nType);

        public static int OpenNetDevice(bool bNet, string devName, Pss_Type nType) => cvPss_Sx_OpenNetDeviceRaw(bNet, devName, nType);

        // 关闭
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_CloseDevice", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvPssSxCloseDevice(int nDevID);
        public static bool CloseDevice(int nDevID) => cvPssSxCloseDevice(nDevID);

        // 获取ID和长度
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_GetIDN", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvPss_Sx_GetIDN(int nDevID, byte[] pszIdn, ref int strLen);
        public static bool cvPssSxGetIDN(int nDevID, byte[] pszIdn, ref int strLen) => cvPss_Sx_GetIDN(nDevID, pszIdn, ref strLen);

        // 以恒压/恒流模式为准设置（设置后缓存标志位供其它方法转换单位）
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetSourceV", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvPssSxSetSourceVRaw(int nDevID, bool isSourceV);
        public static bool cvPssSxSetSourceV(int nDevID, bool isSourceV)
        {
            _isSourceV = isSourceV;
            return cvPssSxSetSourceVRaw(nDevID, isSourceV);
        }

        // 源表硬件设置（4线制/2线制，前后接口）
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_Set4WireFront", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvPss_Sx_Set4WireFront(int nDevID, bool bWier, bool bFront);
        public static bool Set4WireFront(int nDevID, bool bWier, bool bFront) => cvPss_Sx_Set4WireFront(nDevID, bWier, bFront);

        // 设置通道 A 还是 B
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetSrcAorB", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvPss_Sx_SetSrcAorB(int nDevID, bool bSrcA);
        public static bool SetSrcAorB(int nDevID, bool bSrcA) => cvPss_Sx_SetSrcAorB(nDevID, bSrcA);

        // 采集循环里的延迟时间
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetDelayTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvPss_Sx_SetDelayTime(int nDevID, double dTimems);
        public static bool SetDelayTime(int nDevID, double dTimems) => cvPss_Sx_SetDelayTime(nDevID, dTimems);

        // 关闭源输出
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_CloseOutput", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvPss_Sx_CloseOutput(int nDevID);
        public static bool CvPssSxCloseOutput(int nDevID) => cvPss_Sx_CloseOutput(nDevID);

        // 设置电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvMeasureDataRaw(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);

        public static bool cvMeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            if (_isSourceV) return cvMeasureDataRaw(nDevID, measureVal, lmtVal / 1000.0, ref rstV, ref rstI);
            else return cvMeasureDataRaw(nDevID, measureVal / 1000.0, lmtVal, ref rstV, ref rstI);
        }

        // 获取结果
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetMeasureResult", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvGetMeasureResultRaw(int nDevID, ref double rstV, ref double rstI);
        public static bool cvGetMeasureResult(int nDevID, ref double rstV, ref double rstI) => cvGetMeasureResultRaw(nDevID, ref rstV, ref rstI);

        // 设置单步电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvStepMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvStepMeasureDataRaw(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);
        public static bool cvStepMeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            if (_isSourceV) return cvStepMeasureDataRaw(nDevID, measureVal, lmtVal / 1000.0, ref rstV, ref rstI);
            else return cvStepMeasureDataRaw(nDevID, measureVal / 1000.0, lmtVal, ref rstV, ref rstI);
        }

        // 序列扫描 指定量程
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvSweepData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvSweepDataRaw(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList);
        public static bool cvSweepData(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList)
        {
            if (_isSourceV) return cvSweepDataRaw(nDevID, srcRng, lmtRng / 1000.0, lmtVal / 1000.0, startVal, stopVal, points, pVList, pIList);
            else return cvSweepDataRaw(nDevID, srcRng / 1000.0, lmtRng, lmtVal, startVal / 1000.0, stopVal / 1000.0, points, pVList, pIList);
        }

        // 序列扫描 自定义值列表
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvListData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool cvListDataRaw(int nDevID, double srcRng, double lmtRng, double lmtVal, double[] pCustomData, int points, double[] pVList, double[] pIList);
        public static bool cvListData(int nDevID, double srcRng, double lmtRng, double lmtVal, double[] pCustomData, int points, double[] pVList, double[] pIList)
        {
            if (_isSourceV) return cvListDataRaw(nDevID, srcRng, lmtRng / 1000.0, lmtVal / 1000.0, pCustomData, points, pVList, pIList);
            else return cvListDataRaw(nDevID, srcRng / 1000.0, lmtRng, lmtVal, pCustomData, points, pVList, pIList);
        }
    }
}