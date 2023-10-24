#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Windows.Media.Media3D;

namespace ColorVision.Common.Util
{
    public class CVFileUtils
    {
        private const string LIBRARY_CVCommonFile = "CVCommonFileUtils.dll";
        public static byte[] ReadBinaryFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                BinaryReader binaryReader = new BinaryReader(fileStream);
                //获取文件长度
                long length = fileStream.Length;
                byte[] bytes = new byte[length];
                //读取文件中的内容并保存到字节数组中
                binaryReader.Read(bytes, 0, bytes.Length);
                return bytes;
            }
            return null;
        }

        public static void WriteBinaryFile(string fileName, byte[] data)
        {
            using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fileStream))
            {
                writer.Write(data);
            }
        }


        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "ReadCVCIEHeader", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ReadCVCIEHeader(string cieFileName, out int width, out int height,out int bpp, out int channels, out int dataLen, out int srcFileNameLen);

        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "ReadCVCIE", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ReadCVCIE(string cieFileName, float[] exp, byte[] data, int dateLen, StringBuilder srcFileName, int srcFileNameLen);

        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "WriteCVCIE", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int WriteCVCIE(string cieFileName, float[] exp, int width, int height, int bpp, int channels, byte[] data, int dateLen, string srcFileName);
    }
}
