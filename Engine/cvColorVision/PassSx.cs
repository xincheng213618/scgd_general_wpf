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

    public static class PassSx
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        // 打开
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_OpenNetDevice", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int OpenNetDevice([MarshalAs(UnmanagedType.Bool)] bool bNet, [MarshalAs(UnmanagedType.LPStr)] string devName, Pss_Type nType);

        // 关闭
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_CloseDevice", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseDevice(int nDevID);

        // 获取ID和长度
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_GetIDN", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetIDN(int nDevID, [Out] byte[] pszIdn, ref int strLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_GetIDN", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetIDN(int nDevID, StringBuilder pszIdn, ref int strLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetSourceV", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetSourceV(int nDevID, [MarshalAs(UnmanagedType.I1)] bool isSourceV);

        // 源表硬件设置（4线制/2线制，前后接口）
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_Set4WireFront", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int Set4WireFront(int nDevID, [MarshalAs(UnmanagedType.I1)] bool bWier, [MarshalAs(UnmanagedType.I1)] bool bFront);

        // 设置通道 A 还是 B
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetSrcAorB", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetSrcAorB(int nDevID, [MarshalAs(UnmanagedType.I1)] bool bSrcA);

        // 采集循环里的延迟时间
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetDelayTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetDelayTime(int nDevID, double dTimems);

        // 关闭源输出
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_CloseOutput", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseOutput(int nDevID);

        // 设置电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int MeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);

        // 获取结果
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetMeasureResult", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetMeasureResult(int nDevID, ref double rstV, ref double rstI);

        // 设置单步电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvStepMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int StepMeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);

        // 设置单步量程参数并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvStepMeasureDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int StepMeasureDataEx(int nDevID, double srcRng, double lmtRng, double measureVal, double lmtVal, ref double rstV, ref double rstI);

        // 序列扫描 指定量程
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvSweepData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int SweepData(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, [Out] double[] pVList, [Out] double[] pIList);

        // 序列扫描 自动量程
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvSweepDataAutoRng", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int SweepDataAutoRng(int nDevID, double lmtVal, double startVal, double stopVal, int points, [Out] double[] pVList, [Out] double[] pIList);

        // 序列扫描 自定义值列表
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvListData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ListData(int nDevID, double srcRng, double lmtRng, double lmtVal, [In] double[] pCustomData, int points, [Out] double[] pVList, [Out] double[] pIList);
    }
}