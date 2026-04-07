using System;

namespace ColorVision.Core
{
    public static class ImageCompute
    {
        private static bool CheckCudaSupport()
        {
            try
            {
                int result = Nvcuda.cuInit(0);
                if (result != 0)
                {
                    return false;
                }

                result = Nvcuda.cuDeviceGetCount(out int deviceCount);
                if (result != 0 || deviceCount == 0)
                {
                    return false;
                }
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
        /// <summary>
        /// Runtime switch controlled by upper layers (e.g. CUDA initializer/config).
        /// </summary>
        public static bool UseCuda { get; set; } = CheckCudaSupport();

        public static int Fusion(string fusionjson, out HImage hImage)
        {
            if (UseCuda)
            {
                return OpenCVCuda.CM_Fusion(fusionjson, out hImage);
            }
            return OpenCVMediaHelper.M_Fusion(fusionjson, out hImage);
        }
    }
}