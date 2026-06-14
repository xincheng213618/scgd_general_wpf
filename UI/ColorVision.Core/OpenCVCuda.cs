#pragma warning disable CA1401,CA1707,CA2101
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    public static class OpenCVCuda
    {
        private const string LibPath = "opencv_cuda.dll";


        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void M_FreeHImageData(IntPtr data);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_Fusion(string fusionjson, out HImage hImage);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_Fusion(string fusionjson, out HImage hImage);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_Fusion_Async(string fusionjson, out HImage hImage);

        [DllImport(LibPath, EntryPoint = "CM_Fusion_Batch", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CM_Fusion_BatchNative(string batchjson, [Out] HImage[] outImages, int outCapacity, out int outCount);

        public static int CM_Fusion_Batch(string batchjson, HImage[] outImages, out int outCount)
        {
            ArgumentNullException.ThrowIfNull(outImages);
            outCount = 0;
            Array.Clear(outImages);
            return CM_Fusion_BatchNative(batchjson, outImages, outImages.Length, out outCount);
        }

    }
}
