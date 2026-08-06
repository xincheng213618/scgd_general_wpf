#pragma warning disable CA1401,CA1707,CA2101
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    public static class OpenCVCuda
    {
        private const string LibPath = "opencv_cuda.dll";


        [DllImport(LibPath, EntryPoint = "M_FreeHImageData", CallingConvention = CallingConvention.Cdecl)]
        private static extern void M_FreeHImageDataNative(IntPtr data);

        [DllImport(LibPath, EntryPoint = "M_Fusion", CallingConvention = CallingConvention.Cdecl)]
        private static extern int M_FusionNative(string fusionjson, out HImage hImage);

        [DllImport(LibPath, EntryPoint = "CM_Fusion", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CM_FusionNative(string fusionjson, out HImage hImage);

        [DllImport(LibPath, EntryPoint = "CM_Fusion_Async", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CM_FusionAsyncNative(string fusionjson, out HImage hImage);

        [DllImport(LibPath, EntryPoint = "CM_Fusion_Batch", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CM_Fusion_BatchNative(string batchjson, [Out] HImage[] outImages, int outCapacity, out int outCount);

        public static void M_FreeHImageData(IntPtr data)
        {
            PrepareNativeLogging();
            M_FreeHImageDataNative(data);
        }

        public static int M_Fusion(string fusionjson, out HImage hImage)
        {
            PrepareNativeLogging();
            return M_FusionNative(fusionjson, out hImage);
        }

        public static int CM_Fusion(string fusionjson, out HImage hImage)
        {
            PrepareNativeLogging();
            return CM_FusionNative(fusionjson, out hImage);
        }

        public static int CM_Fusion_Async(string fusionjson, out HImage hImage)
        {
            PrepareNativeLogging();
            return CM_FusionAsyncNative(fusionjson, out hImage);
        }

        public static int CM_Fusion_Batch(string batchjson, HImage[] outImages, out int outCount)
        {
            ArgumentNullException.ThrowIfNull(outImages);
            outCount = 0;
            Array.Clear(outImages);
            PrepareNativeLogging();
            return CM_Fusion_BatchNative(batchjson, outImages, outImages.Length, out outCount);
        }

        private static void PrepareNativeLogging()
        {
            try
            {
                NativeLogBridge.PrepareForNativeCall(NativeLogSource.OpencvCuda);
            }
            catch
            {
                // Optional diagnostics must never prevent the native algorithm call.
            }
        }

    }
}
