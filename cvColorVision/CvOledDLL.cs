#pragma warning disable  CA2101,CA1707,CA1401,CA1051,CA1838,CA1711,CS0649,CA2211,CA1708,CA1720
using System.Runtime.InteropServices;

namespace cvColorVision
{
    public class CvOledDLL
    {
        private const string LIBRARY_CVOLED = "cvOled.dll";
        [DllImport(LIBRARY_CVOLED, EntryPoint = "CvOledInit",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CvOledInit();
        [DllImport(LIBRARY_CVOLED, EntryPoint = "CvOledRealse",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CvOledRealse();

        [DllImport(LIBRARY_CVOLED, EntryPoint = "CvLoadParam",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR CvLoadParam(string json);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "loadPictureMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern ulong loadPictureMemLength(string path);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "loadPicture",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int loadPicture(string path, ref int w, ref int h, byte[] imgdata);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "findDotsArray",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR findDotsArray(int w, int h, byte[] imgdata, int type, CVLED_COLOR color);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "rebuildPixels", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR rebuildPixels(int w, int h, byte[] imgdata, int type, CVLED_COLOR color, float exp, string path);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "morieFilter", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR morieFilter(int w, int h, byte[] imgdata, int type, string path);
    }
}