#pragma warning disable CA1401,CA1708,CA1707,CA2101,CA1711,SYSLIB1054
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public static class Algorithms
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
        public static extern int forPoint( byte[] inputim, out int[] xPos,out int[] yPos, int h, int w,int nbpp, int nChannels, int method, ref int pointNumber, int pointDistance, int startPosition, int binaryPercentage );
    }
}
