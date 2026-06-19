#pragma warning disable CA1051
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

    [StructLayout(LayoutKind.Sequential)]
    public struct HImage : IDisposable
    {
        public int rows;
        public int cols;
        public int channels;

        public int depth; //bpp
        public int stride;

        [MarshalAs(UnmanagedType.I1)]
        public bool isDispose;

        public IntPtr pData;

        public void Dispose()
        {
            if (pData != IntPtr.Zero)
            {
                if (!isDispose)
                    Marshal.FreeCoTaskMem(pData);
                pData = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }
}
