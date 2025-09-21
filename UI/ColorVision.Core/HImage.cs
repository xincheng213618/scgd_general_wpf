#pragma warning disable CA1401,CA1051,CA2101,CA1707
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    public struct HImage : IDisposable
    {
        public int rows;
        public int cols;
        public int channels;

        public int depth; //bpp
        public int stride;

        public readonly int Type => (((depth & ((1 << 3) - 1)) + ((channels - 1) << 3)));

        public int ElemSize => ((((((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) *
                        ((0x28442211 >> (((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((1 << 3) - 1)) * 4)) & 15)));

        public readonly uint Size { get => (uint)(rows * cols * channels * (depth / 8)); }

        public IntPtr pData;
        public void Dispose()
        {
            // 使用 Marshal.FreeHGlobal来释放由 Marshal.AllocHGlobal 分配的内存
            if (pData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pData);
                pData = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }
}
