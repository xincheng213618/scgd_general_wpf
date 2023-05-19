using System.Runtime.InteropServices;

namespace cvColorVision
{
    public class PassSx
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";
        private static bool _isSourceV = true;

        //打开
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_OpenNetDevice", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int cvPssSxOpenNetDevice(bool bNet, string devName);

        public static int OpenNetDevice(bool bNet, string devName)=> cvPssSxOpenNetDevice(bNet, devName);

        //关闭
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_CloseDevice", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvPssSxCloseDevice(int nDevID);
        public static bool CloseDevice(int nDevID) => cvPssSxCloseDevice(nDevID);

        //获取ID和长度
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_GetIDN", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvPssSxGetIDN(int nDevID, byte[] pszIdn, ref int strLen);

        //设置电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvMeasureDataRaw(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);

        public static bool cvMeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            if (_isSourceV) return cvMeasureDataRaw(nDevID, measureVal, lmtVal / 1000.0, ref rstV, ref rstI);
            else return cvMeasureDataRaw(nDevID, measureVal / 1000.0, lmtVal, ref rstV, ref rstI);
        }
        //获取结果
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetMeasureResult", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvGetMeasureResult(int nDevID, ref double rstV, ref double rstI);
        //设置单步电压参数等并点亮
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvStepMeasureData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvStepMeasureDataRaw(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI);
        public static bool cvStepMeasureData(int nDevID, double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            if (_isSourceV) return cvStepMeasureDataRaw(nDevID, measureVal, lmtVal / 1000.0, ref rstV, ref rstI);
            else return cvStepMeasureDataRaw(nDevID, measureVal / 1000.0, lmtVal, ref rstV, ref rstI);
        }
        //点亮后执行关闭
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetOutput", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvPssSxSetOutput(int nDevID);
        //点亮后执行关闭
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvPss_Sx_SetSourceV", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvPssSxSetSourceVRaw(int nDevID, bool isSourceV);
        public static bool cvPssSxSetSourceV(int nDevID, bool isSourceV)
        {
            _isSourceV = isSourceV;
            return cvPssSxSetSourceVRaw(nDevID, isSourceV);
        }

        //序列扫描 指定量程
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvSweepData",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool cvSweepDataRaw(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList);
        public static bool cvSweepData(int nDevID, double srcRng, double lmtRng, double lmtVal, double startVal, double stopVal, int points, double[] pVList, double[] pIList)
        {
            if (_isSourceV) return cvSweepDataRaw(nDevID, srcRng, lmtRng / 1000.0, lmtVal / 1000.0, startVal, stopVal, points, pVList, pIList);
            else return cvSweepDataRaw(nDevID, srcRng / 1000.0, lmtRng, lmtVal, startVal / 1000.0, stopVal / 1000.0, points, pVList, pIList);
        }
    }
}


