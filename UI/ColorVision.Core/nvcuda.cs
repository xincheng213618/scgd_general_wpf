using System;
using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    public static class Nvcuda 
    {
        [DllImport("nvcuda.dll")]
        public static extern int cuInit(uint Flags);

        [DllImport("nvcuda.dll")]
        public static extern int cuDeviceGetCount(out int count);

        [DllImport("nvcuda.dll")]
        public static extern int cuDeviceGetName(byte[] name, int len, int dev);

        [DllImport("nvcuda.dll")]
        public static extern int cuDeviceComputeCapability(out int major, out int minor, int dev);

        [DllImport("nvcuda.dll")]
        public static extern int cuDeviceTotalMem(out ulong bytes, int dev);

        [DllImport("nvcuda.dll", EntryPoint = "cuDeviceTotalMem_v2")]
        public static extern int cuDeviceTotalMem_v2(out ulong bytes, int device);




    }
}
