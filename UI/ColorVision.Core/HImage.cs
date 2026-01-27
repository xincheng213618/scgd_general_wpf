using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ColorVision.Core
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RoiRect
    {
        public RoiRect()
        {
            X  = 0; Y = 0; Width = 0; Height = 0;
        }
        public RoiRect(int x,int y,int width,int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
        public RoiRect(Rect rect)
        {
            X = (int)rect.X; Y = (int)rect.Y; Width = (int)rect.Width; Height = (int)rect.Height;
        }
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    public struct HImage : IDisposable
    {
        public int rows;
        public int cols;
        public int channels;

        public int depth; //bpp
        public int stride;

        public readonly int Type => (((depth & ((1 << 3) - 1)) + ((channels - 1) << 3)));

        public readonly int ElemSize => ((((((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) *
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
