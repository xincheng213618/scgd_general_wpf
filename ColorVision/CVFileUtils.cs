#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CS8605,CA1707
namespace ColorVision
{
    /*
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
                    fileInfo.Channels = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    fileInfo.Width = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    fileInfo.Height = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                   int dataLen = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    if (dataLen > 0)
                    {
                        fileInfo.Data = new byte[dataLen];
                        Buffer.BlockCopy(fileData, startIndex, fileInfo.Data, 0, dataLen);

                        return true;
                    }
                }
            }

            return false;
        }

        public static int GetMatDepth(int bpp)
        {
            int depth = 0;
            switch (bpp)
            {
                case 8:
                    depth = 0;
                    break;
                case 16:
                    depth = 2;
                    break;
                case 32:
                    depth = 5;
                    break;
                case 64:
                    depth = 6;
                    break;
            }

            return depth;
        }

        public static CVCIEFileInfo WriteBinaryFile_CVRGB(string fullFileName, byte[] bytes)
        {
            CVCIEFileInfo fileInfo = new CVCIEFileInfo();
            var src = OpenCvSharp.Cv2.ImDecode(bytes, OpenCvSharp.ImreadModes.Unchanged);
            OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
            int depth = src.Depth();
            fileInfo.Channels = src.Channels();
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
                    src.ConvertTo(dst, OpenCvSharp.MatType.MakeType(0, fileInfo.Channels), 255.0 / 65535, 0.5);
                    break;
                case 3:
                    //text = "CV_16S";
                    src.ConvertTo(dst, OpenCvSharp.MatType.MakeType(1, fileInfo.Channels), 255.0 / 65535, 0.5);
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
            fileInfo.Width = src.Width;
            fileInfo.Height = src.Height;
            int rows = src.Rows, cols = src.Cols;
            fileInfo.Data = new byte[rows * cols * fileInfo.Channels];
            Marshal.Copy(dst.Data, fileInfo.Data, 0, fileInfo.Data.Length);
            WriteBinaryFile_CVRGB(fullFileName,src.Width,src.Height, fileInfo.Channels, fileInfo.Data);

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
    */
}
