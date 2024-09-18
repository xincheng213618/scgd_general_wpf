#pragma warning disable  CA2101,CA1707,CA1401,CA1051,CA1838,CA1711,CS0649,CA2211,CA1708,CA1720
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    public class KBDLL
    {
        private const string LIBRARY_KB = "cvCameraKb.dll";
        [DllImport(LIBRARY_KB, EntryPoint = "createResultCsv", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void createResultCsv(string cfg_path, string rst_path);
        [DllImport(LIBRARY_KB, EntryPoint = "processKeyborad",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void processKeyborad(string img_path, string cfg_path, string rst_path, string setting_json, string serial_no, float exp, uint w, uint h, int PicPart, double correctData, string correctPath);
        [DllImport(LIBRARY_KB, EntryPoint = "getPassFail",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void getPassFail(StringBuilder pass);
        [DllImport(LIBRARY_KB, EntryPoint = "calculateHalo",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double calculateHalo(uint w, uint h, byte[] rawArray, int x, int y, int width, int height, int outMOVE);
        [DllImport(LIBRARY_KB, EntryPoint = "InitResource",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void InitResource();
        [DllImport(LIBRARY_KB, EntryPoint = "CM_ExportToTIFF",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ExportToTIFF(string fileName, uint w, uint h, byte[] rawArray, ulong buflen, bool iscolor, float img_rotate_angle);

    }
}