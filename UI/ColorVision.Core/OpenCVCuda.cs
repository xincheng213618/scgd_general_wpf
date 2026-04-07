using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    public static class OpenCVCuda
    {
        private const string LibPath = "opencv_cuda.dll";


        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_Fusion(string fusionjson, out HImage hImage);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_Fusion_Async(string fusionjson, out HImage hImage);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_Fusion_Batch(string batchjson, out System.IntPtr outImages, out int outCount);

    }
}
