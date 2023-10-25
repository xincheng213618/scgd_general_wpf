using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows.Media.Media3D;

namespace ColorVision.Common.Util
{
    public struct C_CVCIEFileInfo
    {
        public int width;
        public int height;
        public int bpp;
        public int channels;
        public int gain;
        public IntPtr exp;
        public IntPtr srcFileName;
        public int srcFileNameLen;
        public IntPtr data;
        public int dataLen;
    }
    public struct CVCIEFileInfo
    {
        public int width;
        public int height;
        public int bpp;
        public int channels;
        public int gain;
        public float[] exp;
        public string srcFileName;
        public byte[] data;
    }
    public static class CVFileUtils
    {
        private const int MAX_DIRECTORY_PATH = 1024;
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
        public unsafe static extern int C_ReadCVCIEHeader(string cieFileName, IntPtr fileInfo);

        /// <summary>
        /// 读取CVCIE文件头信息
        /// </summary>
        /// <param name="cieFileName"></param>
        /// <param name="fileInfo"></param>
        /// <returns>
        ///  返回值:
        ///  0 : 成功
        /// -1 : 文件头非法
        /// -2 : 文件版本非法
        /// -999 : 文件不存在
        /// </returns>
        public static int ReadCVCIEHeader(string cieFileName, ref CVCIEFileInfo fileInfo)
        {
            C_CVCIEFileInfo c_fileInfo = new C_CVCIEFileInfo();
            c_fileInfo.srcFileNameLen = MAX_DIRECTORY_PATH;
            c_fileInfo.srcFileName = Marshal.AllocHGlobal(c_fileInfo.srcFileNameLen);
            IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(C_CVCIEFileInfo)));
            Marshal.StructureToPtr(c_fileInfo, fileInfoPtr, true);
            int ret = CVFileUtils.C_ReadCVCIEHeader(cieFileName, fileInfoPtr);
            if (ret == 0)
            {
                c_fileInfo = (C_CVCIEFileInfo)Marshal.PtrToStructure(fileInfoPtr, typeof(C_CVCIEFileInfo));
                fileInfo.width = c_fileInfo.width;
                fileInfo.height = c_fileInfo.height;
                fileInfo.bpp = c_fileInfo.bpp;
                fileInfo.channels = c_fileInfo.channels;
                fileInfo.gain = c_fileInfo.gain;
                byte[] buffer = new byte[c_fileInfo.srcFileNameLen];
                Marshal.Copy(c_fileInfo.srcFileName, buffer, 0, buffer.Length);
                fileInfo.srcFileName = System.Text.Encoding.Default.GetString(buffer);
            }
            Marshal.FreeHGlobal(c_fileInfo.srcFileName);
            Marshal.FreeHGlobal(fileInfoPtr);
            return ret;
        }
        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "ReadCVCIE", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int C_ReadCVCIE(string cieFileName, IntPtr fileInfo);

        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "WriteCVCIE", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int C_WriteCVCIE(string cieFileName, IntPtr fileInfo);

        /// <summary>
        /// 写CVCIE文件
        /// </summary>
        /// <param name="cieFileName"></param>
        /// <param name="fileInfo"></param>
        /// <returns>
        /// 返回值:
        /// * 0 : 写入成功
        ///  -1 : 写入失败
        /// </returns>
        public static int WriteCVCIE(string cieFileName, CVCIEFileInfo fileInfo)
        {
            C_CVCIEFileInfo c_fileInfo = new C_CVCIEFileInfo();
            c_fileInfo.width = fileInfo.width;
            c_fileInfo.height = fileInfo.height;
            c_fileInfo.bpp = fileInfo.bpp;
            c_fileInfo.channels = fileInfo.channels;
            c_fileInfo.gain = fileInfo.gain;
            //源文件名
            byte[] srcFileNameBytes = System.Text.Encoding.Default.GetBytes(fileInfo.srcFileName);
            c_fileInfo.srcFileNameLen = srcFileNameBytes.Length;
            c_fileInfo.srcFileName = Marshal.AllocHGlobal(c_fileInfo.srcFileNameLen);
            Marshal.Copy(srcFileNameBytes, 0, c_fileInfo.srcFileName, srcFileNameBytes.Length);
            //曝光
            c_fileInfo.exp = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(float)) * fileInfo.exp.Length);
            Marshal.Copy(fileInfo.exp, 0, c_fileInfo.exp, fileInfo.exp.Length);
            //数据
            c_fileInfo.dataLen = fileInfo.data.Length;
            c_fileInfo.data = Marshal.AllocHGlobal(c_fileInfo.dataLen);
            Marshal.Copy(fileInfo.data, 0, c_fileInfo.data, fileInfo.data.Length);
            //
            IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(C_CVCIEFileInfo)));
            Marshal.StructureToPtr(c_fileInfo, fileInfoPtr, true);
            int ret = C_WriteCVCIE(cieFileName, fileInfoPtr);

            Marshal.FreeHGlobal(c_fileInfo.exp);
            Marshal.FreeHGlobal(c_fileInfo.srcFileName);
            Marshal.FreeHGlobal(c_fileInfo.data);

            Marshal.FreeHGlobal(fileInfoPtr);
            return ret;
        }


        [DllImport(LIBRARY_CVCommonFile, EntryPoint = "GetCVCIEFileLength", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GetCVCIEFileLength(string cieFileName);

        /// <summary>
        /// 读取CVCIE文件
        /// </summary>
        /// <param name="cieFileName"></param>
        /// <param name="fileInfo"></param>
        /// <returns>
        ///  返回值:
        ///  0 : 成功
        /// -1 : 文件头非法
        /// -2 : 文件版本非法
        /// -3 : 数据区长度不够
        /// -999 : 文件不存在
        /// </returns>
        public static int ReadCVCIE(string cieFileName, ref CVCIEFileInfo fileInfo)
        {
            long fileLen = CVFileUtils.GetCVCIEFileLength(cieFileName);
            if (fileLen > 0)
            {
                C_CVCIEFileInfo c_fileInfo = new C_CVCIEFileInfo();
                c_fileInfo.exp = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(float)) * 3);
                c_fileInfo.srcFileNameLen = MAX_DIRECTORY_PATH;
                c_fileInfo.srcFileName = Marshal.AllocHGlobal(c_fileInfo.srcFileNameLen);


                c_fileInfo.dataLen = (int)fileLen;
                c_fileInfo.data = Marshal.AllocHGlobal(c_fileInfo.dataLen);
                IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(C_CVCIEFileInfo)));
                Marshal.StructureToPtr(c_fileInfo, fileInfoPtr, true);
                int ret = CVFileUtils.C_ReadCVCIE(cieFileName, fileInfoPtr);
                if (ret == 0)
                {
                    c_fileInfo = (C_CVCIEFileInfo)Marshal.PtrToStructure(fileInfoPtr, typeof(C_CVCIEFileInfo));
                    fileInfo.width = c_fileInfo.width;
                    fileInfo.height = c_fileInfo.height;
                    fileInfo.bpp = c_fileInfo.bpp;
                    fileInfo.channels = c_fileInfo.channels;
                    fileInfo.gain = c_fileInfo.gain;
                    byte[] buffer = new byte[c_fileInfo.srcFileNameLen];
                    Marshal.Copy(c_fileInfo.srcFileName, buffer, 0, buffer.Length);
                    fileInfo.srcFileName = System.Text.Encoding.Default.GetString(buffer);
                    fileInfo.exp = new float[3];
                    Marshal.Copy(c_fileInfo.exp, fileInfo.exp, 0, fileInfo.exp.Length);
                    fileInfo.data = new byte[c_fileInfo.dataLen];
                    Marshal.Copy(c_fileInfo.data, fileInfo.data, 0, fileInfo.data.Length);
                }
                Marshal.FreeHGlobal(c_fileInfo.exp);
                Marshal.FreeHGlobal(c_fileInfo.srcFileName);
                Marshal.FreeHGlobal(c_fileInfo.data);

                Marshal.FreeHGlobal(fileInfoPtr);

                return ret;
            }

            return -999;
        }

        public static int ConvertXYZToCVCIE(string cieFileName, string xFileName, string yFileName, string zFileName, ref CVCIEFileInfo fileInfo)
        {
            int ret = -999;
            if (System.IO.File.Exists(fileInfo.srcFileName) && System.IO.File.Exists(xFileName) && System.IO.File.Exists(yFileName) && System.IO.File.Exists(zFileName))
            {
                var x = OpenCvSharp.Cv2.ImRead(xFileName, OpenCvSharp.ImreadModes.Unchanged);
                var y = OpenCvSharp.Cv2.ImRead(yFileName, OpenCvSharp.ImreadModes.Unchanged);
                var z = OpenCvSharp.Cv2.ImRead(zFileName, OpenCvSharp.ImreadModes.Unchanged);
                OpenCvSharp.Mat[] src = new OpenCvSharp.Mat[3] { x, y, z };
                fileInfo.width = x.Cols; fileInfo.height = x.Rows;
                fileInfo.channels = 3;
                fileInfo.bpp = 32;
                fileInfo.data = new byte[fileInfo.width * fileInfo.height * fileInfo.channels * 4];
                OpenCvSharp.Mat mergeDst = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.Merge(src, mergeDst);
                Marshal.Copy(mergeDst.Data, fileInfo.data, 0, fileInfo.data.Length);

                WriteCVCIE(cieFileName, fileInfo);
            }

            return ret;
        }
    }
}
