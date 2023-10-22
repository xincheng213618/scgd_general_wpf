#pragma warning disable CA1401
using System;
using System.Runtime.InteropServices;

namespace ColorVision
{
    public static class OpenCVHelper
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void RtlMoveMemory(IntPtr Destination, IntPtr Source, uint Length);

        [DllImport("OpenCVHelper.dll", CharSet = CharSet.Unicode)]
        public static extern void ReadCVFile(string FullPath);

        [DllImport("OpenCVHelper.dll")]
        public unsafe static extern void SetInitialFrame(nint pRoutineHandler);

        [DllImport("OpenCVHelper.dll", CharSet = CharSet.Unicode)]
        public static extern void ReadVideoTest(string FullPath);



    }
}
