#pragma warning disable CA1401,CA1051
using System;
using System.Runtime.InteropServices;

namespace ColorVision
{
    public struct HImage
    {
        public int rows;
        public int cols;
        public int channels;
        public int depth; //Bpp

        public readonly int Type
        {
            get { return (((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))); }
        }

        public readonly int ElemSize
        {
            get
            {
                return ((((((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) *
                        ((0x28442211 >> (((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((1 << 3) - 1)) * 4)) & 15)));
            }
        }

        public IntPtr pData;
    }

    public static class OpenCVHelper
    {

        [DllImport("ColorVisionCore.dll", CharSet = CharSet.Unicode)]
        public static extern int CVWrite(string FullPath, HImage hImage,int compression =0);

        [DllImport("ColorVisionCore.dll", CharSet = CharSet.Unicode)]
        public static extern int CVRead(string FullPath, out HImage hImage);


        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void RtlMoveMemory(IntPtr Destination, IntPtr Source, uint Length);

        [DllImport("OpenCVHelper.dll", CharSet = CharSet.Unicode)]
        public static extern void ReadCVFile(string FullPath);

        [DllImport("OpenCVHelper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ReadGhostImage([MarshalAs(UnmanagedType.LPStr)] string FilePath, out HImage hImage);

        [DllImport("OpenCVHelper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ReadGhostHImage(HImage image, out HImage hImage);


        [DllImport("OpenCVHelper.dll")]
        public unsafe static extern void SetInitialFrame(nint pRoutineHandler);

        [DllImport("OpenCVHelper.dll")]
        public static extern void ReadVideoTest(string FullPath);



    }
}
