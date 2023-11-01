#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows.Media.Media3D;

namespace ColorVision
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

        public static bool ReadBinaryFile_CVRGB(string fullFileName, out CVCIEFileInfo fileInfo)
        {
            fileInfo = new CVCIEFileInfo();
            byte[] fileData = ReadBinaryFile(fullFileName);
            int startIndex = 0;
            if (fileData[0] == 'C' && fileData[1] == 'V' && fileData[2] == 'R' && fileData[3] == 'G' && fileData[4] == 'B')
            {
                startIndex += 5;
                uint ver = BitConverter.ToUInt32(fileData, startIndex);
                startIndex += 4;
                if (ver == 1)
                {
                    fileInfo.channels = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    fileInfo.width = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    fileInfo.height = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                   int dataLen = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    if (dataLen > 0)
                    {
                        fileInfo.data = new byte[dataLen];
                        Buffer.BlockCopy(fileData, startIndex, fileInfo.data, 0, dataLen);

                        return true;
                    }
                }
            }

            return false;
        }

        public static CVCIEFileInfo WriteBinaryFile_CVRGB(string fullFileName, byte[] bytes)
        {
            CVCIEFileInfo fileInfo = new CVCIEFileInfo();
            var src = OpenCvSharp.Cv2.ImDecode(bytes, OpenCvSharp.ImreadModes.Unchanged);
            OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
            int depth = src.Depth();
            fileInfo.channels = src.Channels();
            switch (depth)
            {
                case 0:
                    //text = "CV_8U";
                    //type = 1;
                    break;
                case 1:
                    //text = "CV_8S";
                    //type = 1;
                    break;
                case 2:
                    //text = "CV_16U";
                    src.ConvertTo(dst, OpenCvSharp.MatType.MakeType(0, fileInfo.channels), 255.0 / 65535, 0.5);
                    break;
                case 3:
                    //text = "CV_16S";
                    src.ConvertTo(dst, OpenCvSharp.MatType.MakeType(1, fileInfo.channels), 255.0 / 65535, 0.5);
                    break;
                case 4:
                    //text = "CV_32S";
                    break;
                case 5:
                    //text = "CV_32F";
                    break;
                case 6:
                    //text = "CV_64F";
                    break;
            }
            fileInfo.width = src.Width;
            fileInfo.height = src.Height;
            int rows = src.Rows, cols = src.Cols;
            fileInfo.data = new byte[rows * cols * fileInfo.channels];
            Marshal.Copy(dst.Data, fileInfo.data, 0, fileInfo.data.Length);
            WriteBinaryFile_CVRGB(fullFileName,src.Width,src.Height, fileInfo.channels, fileInfo.data);

            return fileInfo;
        }

        public static void WriteBinaryFile_CVRGB(string fileName, int w, int h, int channels, byte[] data)
        {
            using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fileStream))
            {
                int ver = 1;
                char[] hd = { 'C', 'V', 'R', 'G', 'B' };
                writer.Write(hd);
                writer.Write(ver);
                writer.Write(channels);
                writer.Write(w);
                writer.Write(h);
                //
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        public static void WriteBinaryFile_CVCIE(string fileName, int gain, float[] exp, int w, int h, int bpp, int channels, byte[] dst, string srcFileName)
        {
            using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fileStream))
            {
                int ver = 1;
                char[] hd = { 'C', 'V', 'C', 'I', 'E' };
                writer.Write(hd);
                writer.Write(ver);
                byte[] srcFileNameBytes = System.Text.Encoding.Default.GetBytes(srcFileName);
                writer.Write(srcFileNameBytes.Length);
                writer.Write(srcFileNameBytes);
                writer.Write(gain);
                writer.Write(channels);
                for (int i = 0; i < exp.Length; i++)
                {
                    writer.Write(exp[i]);
                }
                writer.Write(w);
                writer.Write(h);
                writer.Write(bpp);
                //
                writer.Write(dst.Length);
                writer.Write(dst);
            }
        }
    }
}
