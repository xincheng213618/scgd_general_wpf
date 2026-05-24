#pragma warning disable CA1707,CA1711,CA1712,CA1401,CA1051,CA2101,CA1838,CA1806
using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
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
