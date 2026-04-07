namespace ColorVision.Core
{
    public static class ImageCompute
    {
        /// <summary>
        /// Runtime switch controlled by upper layers (e.g. CUDA initializer/config).
        /// </summary>
        public static bool UseCuda { get; set; }

        public static int Fusion(string fusionjson, out HImage hImage)
        {
            if (UseCuda)
            {
                try
                {
                    return OpenCVCuda.CM_Fusion(fusionjson, out hImage);
                }
                catch (System.DllNotFoundException)
                {
                    UseCuda = false;
                }
                catch (System.EntryPointNotFoundException)
                {
                    UseCuda = false;
                }
            }

            return OpenCVMediaHelper.M_Fusion(fusionjson, out hImage);
        }
    }
}