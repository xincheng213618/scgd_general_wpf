using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorVision.Common.Util
{
    public struct CVCIEFileInfo
    {
        public int width;
        public int height;
        public int bpp;
        public int channels;
        public float[] exp;
        public char[] srcFileName;
        public int srcFileNameLen;
        public byte[] data;
        public int dataLen;
    }
    public static class CVFileUtils
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
        public unsafe static extern int ReadCVCIEHeader(string cieFileName, out CVCIEFileInfo fileInfo);

        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "ReadCVCIE", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ReadCVCIE(string cieFileName, out CVCIEFileInfo fileInfo);

        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "WriteCVCIE", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int WriteCVCIE(string cieFileName, CVCIEFileInfo fileInfo);

        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "ReadCVCIEByOne", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ReadCVCIEByOne(char* cieFileName, out CVCIEFileInfo fileInfo);
    }
}
