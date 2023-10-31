#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CS8605,CA1707
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows.Media.Media3D;

namespace ColorVision
{
    public struct C_CVCIEFileInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Bpp { get; set; }
        public int Channels { get; set; }
        public int Gain { get; set; }
        public IntPtr Exp { get; set; }
        public IntPtr SrcFileName { get; set; }
        public int SrcFileNameLen { get; set; }
        public IntPtr Data { get; set; }
        public int DataLen { get; set; }
    }
    public struct CVCIEFileInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Bpp { get; set; }
        public int Channels { get; set; }
        public int Gain { get; set; }
        public float[] Exp { get; set; }
        public string SrcFileName { get; set; }
        public byte[] Data { get; set; }
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
            c_fileInfo.SrcFileNameLen = MAX_DIRECTORY_PATH;
            c_fileInfo.SrcFileName = Marshal.AllocHGlobal(c_fileInfo.SrcFileNameLen);
            IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(C_CVCIEFileInfo)));
            Marshal.StructureToPtr(c_fileInfo, fileInfoPtr, true);
            int ret = CVFileUtils.C_ReadCVCIEHeader(cieFileName, fileInfoPtr);
            if (ret == 0)
            {
                c_fileInfo = (C_CVCIEFileInfo)Marshal.PtrToStructure(fileInfoPtr, typeof(C_CVCIEFileInfo));
                fileInfo.Width = c_fileInfo.Width;
                fileInfo.Height = c_fileInfo.Height;
                fileInfo.Bpp = c_fileInfo.Bpp;
                fileInfo.Channels = c_fileInfo.Channels;
                fileInfo.Gain = c_fileInfo.Gain;
                byte[] buffer = new byte[c_fileInfo.SrcFileNameLen];
                Marshal.Copy(c_fileInfo.SrcFileName, buffer, 0, buffer.Length);
                fileInfo.SrcFileName = System.Text.Encoding.Default.GetString(buffer);
            }
            Marshal.FreeHGlobal(c_fileInfo.SrcFileName);
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
            c_fileInfo.Width = fileInfo.Width;
            c_fileInfo.Height = fileInfo.Height;
            c_fileInfo.Bpp = fileInfo.Bpp;
            c_fileInfo.Channels = fileInfo.Channels;
            c_fileInfo.Gain = fileInfo.Gain;
            //源文件名
            byte[] srcFileNameBytes = System.Text.Encoding.Default.GetBytes(fileInfo.SrcFileName);
            c_fileInfo.SrcFileNameLen = srcFileNameBytes.Length;
            c_fileInfo.SrcFileName = Marshal.AllocHGlobal(c_fileInfo.SrcFileNameLen);
            Marshal.Copy(srcFileNameBytes, 0, c_fileInfo.SrcFileName, srcFileNameBytes.Length);
            //曝光
            c_fileInfo.Exp = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(float)) * fileInfo.Exp.Length);
            Marshal.Copy(fileInfo.Exp, 0, c_fileInfo.Exp, fileInfo.Exp.Length);
            //数据
            c_fileInfo.DataLen = fileInfo.Data.Length;
            c_fileInfo.Data = Marshal.AllocHGlobal(c_fileInfo.DataLen);
            Marshal.Copy(fileInfo.Data, 0, c_fileInfo.Data, fileInfo.Data.Length);
            //
            IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(C_CVCIEFileInfo)));
            Marshal.StructureToPtr(c_fileInfo, fileInfoPtr, true);
            int ret = C_WriteCVCIE(cieFileName, fileInfoPtr);

            Marshal.FreeHGlobal(c_fileInfo.Exp);
            Marshal.FreeHGlobal(c_fileInfo.SrcFileName);
            Marshal.FreeHGlobal(c_fileInfo.Data);

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
                c_fileInfo.Exp = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(float)) * 3);
                c_fileInfo.SrcFileNameLen = MAX_DIRECTORY_PATH;
                c_fileInfo.SrcFileName = Marshal.AllocHGlobal(c_fileInfo.SrcFileNameLen);


                c_fileInfo.DataLen = (int)fileLen;
                c_fileInfo.Data = Marshal.AllocHGlobal(c_fileInfo.DataLen);
                IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(C_CVCIEFileInfo)));
                Marshal.StructureToPtr(c_fileInfo, fileInfoPtr, true);
                int ret = CVFileUtils.C_ReadCVCIE(cieFileName, fileInfoPtr);
                if (ret == 0)
                {
                    c_fileInfo = (C_CVCIEFileInfo)Marshal.PtrToStructure(fileInfoPtr, typeof(C_CVCIEFileInfo));
                    fileInfo.Width = c_fileInfo.Width;
                    fileInfo.Height = c_fileInfo.Height;
                    fileInfo.Bpp = c_fileInfo.Bpp;
                    fileInfo.Channels = c_fileInfo.Channels;
                    fileInfo.Gain = c_fileInfo.Gain;
                    byte[] buffer = new byte[c_fileInfo.SrcFileNameLen];
                    Marshal.Copy(c_fileInfo.SrcFileName, buffer, 0, buffer.Length);
                    fileInfo.SrcFileName = System.Text.Encoding.Default.GetString(buffer);
                    fileInfo.Exp = new float[3];
                    Marshal.Copy(c_fileInfo.Exp, fileInfo.Exp, 0, fileInfo.Exp.Length);
                    fileInfo.Data = new byte[c_fileInfo.DataLen];
                    Marshal.Copy(c_fileInfo.Data, fileInfo.Data, 0, fileInfo.Data.Length);
                }
                Marshal.FreeHGlobal(c_fileInfo.Exp);
                Marshal.FreeHGlobal(c_fileInfo.SrcFileName);
                Marshal.FreeHGlobal(c_fileInfo.Data);

                Marshal.FreeHGlobal(fileInfoPtr);

                return ret;
            }

            return -999;
        }

        public static int ConvertXYZToCVCIE(string cieFileName, string xFileName, string yFileName, string zFileName, ref CVCIEFileInfo fileInfo)
        {
            int ret = -999;
            if (System.IO.File.Exists(fileInfo.SrcFileName) && System.IO.File.Exists(xFileName) && System.IO.File.Exists(yFileName) && System.IO.File.Exists(zFileName))
            {
                var x = OpenCvSharp.Cv2.ImRead(xFileName, OpenCvSharp.ImreadModes.Unchanged);
                var y = OpenCvSharp.Cv2.ImRead(yFileName, OpenCvSharp.ImreadModes.Unchanged);
                var z = OpenCvSharp.Cv2.ImRead(zFileName, OpenCvSharp.ImreadModes.Unchanged);
                OpenCvSharp.Mat[] src = new OpenCvSharp.Mat[3] { x, y, z };
                fileInfo.Width = x.Cols; fileInfo.Height = x.Rows;
                fileInfo.Channels = 3;
                fileInfo.Bpp = 32;
                fileInfo.Data = new byte[fileInfo.Width * fileInfo.Height * fileInfo.Channels * 4];
                OpenCvSharp.Mat mergeDst = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.Merge(src, mergeDst);
                Marshal.Copy(mergeDst.Data, fileInfo.Data, 0, fileInfo.Data.Length);

                WriteCVCIE(cieFileName, fileInfo);
            }

            return ret;
        }
    }
}
