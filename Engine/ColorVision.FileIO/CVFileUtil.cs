#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace ColorVision.FileIO
{
    /// <summary>
    /// 图像通道类型枚举。
    /// </summary>
    public enum CVImageChannelType
    {
        SRC = 0,
        RGB_R = 1,
        RGB_G = 2,
        RGB_B = 3,
        CIE_XYZ_X = 10,
        CIE_XYZ_Y = 11,
        CIE_XYZ_Z = 12,
        CIE_Lv = 13,
        CIE_x = 14,
        CIE_y = 15,
        CIE_u = 16,
        CIE_v = 17
    }

    /// <summary>
    /// CVCIE文件操作工具类，支持CVCIE格式的读写、通道提取等。
    /// </summary>
    public static class CVFileUtil
    {
        private const string MagicHeader = "CVCIE";
        private const int HeaderSize = 5;
        private const int MinimumFileSize = HeaderSize + 4; // Minimum file size to contain the header and version
        private static readonly Encoding Encoding1 = Encoding.GetEncoding("GBK");

        /// <summary>
        /// 判断文件是否为CVCIE格式。
        /// </summary>
        public static bool IsCIEFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < HeaderSize) return false;
                    string fileHeader = new string(br.ReadChars(HeaderSize));
                    return fileHeader == MagicHeader;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IsCIEFile] Exception: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 判断字节数组是否为CVCIE格式。
        /// </summary>
        public static bool IsCIEFile(byte[] fileData)
        {
            if (fileData == null || fileData.Length < HeaderSize) return false;
            string fileHeader = Encoding.ASCII.GetString(fileData, 0, HeaderSize);
            return fileHeader == MagicHeader;
        }

        /// <summary>
        /// 读取CVCIE文件头信息（文件路径）。
        /// </summary>
        public static int ReadCIEFileHeader(string filePath, out CVCIEFile cvcie)
        {
            cvcie = default(CVCIEFile);
            if (!File.Exists(filePath)) return -1;
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 9) return -1;
                    string fileHeader = new string(br.ReadChars(HeaderSize));
                    if (fileHeader != MagicHeader) return -1;
                    cvcie.FileExtType = filePath.Contains(".cvraw") ? CVType.Raw : filePath.Contains(".cvsrc") ? CVType.Src : CVType.CIE;
                    uint ver = (cvcie.version = br.ReadUInt32());
                    if (ver != 1 && ver != 2) return -1;
                    int fileNameLen = br.ReadInt32();
                    if (fileNameLen > 0 && fs.Position + fileNameLen <= fs.Length)
                        cvcie.srcFileName = new string(br.ReadChars(fileNameLen));
                    cvcie.gain = br.ReadSingle();
                    cvcie.channels = (int)br.ReadUInt32();
                    if (fs.Position + cvcie.channels * 4 > fs.Length) return -1;
                    cvcie.exp = new float[cvcie.channels];
                    for (int i = 0; i < cvcie.channels; i++)
                        cvcie.exp[i] = br.ReadSingle();
                    cvcie.rows = (int)br.ReadUInt32();
                    cvcie.cols = (int)br.ReadUInt32();
                    cvcie.bpp = (int)br.ReadUInt32();
                    cvcie.FilePath = filePath;
                    return (int)fs.Position;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileHeader] Exception: {ex}");
                return -1;
            }
        }

        /// <summary>
        /// 读取CVCIE文件头信息（字节数组）。
        /// </summary>
        public static int ReadCIEFileHeader(byte[] fileData, out CVCIEFile cvcie)
        {
            cvcie = default(CVCIEFile);
            if (fileData == null || fileData.Length < 9) return -1;
            try
            {
                string fileHeader = Encoding.ASCII.GetString(fileData, 0, HeaderSize);
                if (fileHeader != MagicHeader) return -1;
                int startIndex = HeaderSize;
                uint version = (cvcie.version = BitConverter.ToUInt32(fileData, startIndex));
                if (version != 1 && version != 2) return -1;
                startIndex += 4;
                int fileNameLength = BitConverter.ToInt32(fileData, startIndex);
                startIndex += 4;
                if (fileNameLength < 0 || startIndex + fileNameLength > fileData.Length) return -1;
                cvcie.srcFileName = Encoding1.GetString(fileData, startIndex, fileNameLength);
                startIndex += fileNameLength;
                if (!TryReadSingle(fileData, ref startIndex, out cvcie.gain)) return -1;
                if (!TryReadUInt32(fileData, ref startIndex, out cvcie.channels)) return -1;
                if (startIndex + cvcie.channels * 4 > fileData.Length) return -1;
                cvcie.exp = new float[cvcie.channels];
                for (int i = 0; i < cvcie.channels; i++)
                {
                    cvcie.exp[i] = BitConverter.ToSingle(fileData, startIndex);
                    startIndex += 4;
                }
                if (!TryReadUInt32(fileData, ref startIndex, out cvcie.rows)) return -1;
                if (!TryReadUInt32(fileData, ref startIndex, out cvcie.cols)) return -1;
                if (!TryReadUInt32(fileData, ref startIndex, out cvcie.bpp)) return -1;
                return startIndex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileHeader(byte[])] Exception: {ex}");
                return -1;
            }
        }

        /// <summary>
        /// 读取CVCIE文件数据部分（文件路径）。
        /// </summary>
        public static bool ReadCIEFileData(string filePath, ref CVCIEFile fileInfo, int dataStartIndex)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < dataStartIndex) return false;
                    fs.Position = dataStartIndex;
                    uint version = fileInfo.version;
                    long dataLen = (version != 2) ? br.ReadInt32() : br.ReadInt64();
                    if (dataLen > 0 && fs.Position + dataLen <= fs.Length)
                    {
                        try
                        {
                            fileInfo.data = new byte[dataLen];
                        }
                        catch (OutOfMemoryException oom)
                        {
                            Debug.WriteLine($"[ReadCIEFileData] OutOfMemoryException: {oom}");
                            return false;
                        }
                        long totalBytesRead = 0;
                        int bytesRead;
                        while (totalBytesRead < dataLen)
                        {
                            int bytesToRead = (int)Math.Min(81920L, dataLen - totalBytesRead);
                            bytesRead = br.Read(fileInfo.data, (int)totalBytesRead, bytesToRead);
                            if (bytesRead == 0) break;
                            totalBytesRead += bytesRead;
                        }
                        if (totalBytesRead == dataLen) return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileData] Exception: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 读取CVCIE文件数据部分（字节数组）。
        /// </summary>
        public static bool ReadCIEFileData(byte[] fileData, ref CVCIEFile fileInfo, int dataStartIndex)
        {
            if (fileData == null || fileData.Length < dataStartIndex) return false;
            try
            {
                using (MemoryStream ms = new MemoryStream(fileData))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    ms.Position = dataStartIndex;
                    if (ms.Length - ms.Position < 4) return false;
                    uint version = fileInfo.version;
                    long dataLen = (version != 2) ? br.ReadInt32() : br.ReadInt64();
                    if (dataLen > 0 && ms.Position + dataLen <= ms.Length)
                    {
                        try
                        {
                            fileInfo.data = new byte[dataLen];
                        }
                        catch (OutOfMemoryException oom)
                        {
                            Debug.WriteLine($"[ReadCIEFileData(byte[])] OutOfMemoryException: {oom}");
                            return false;
                        }
                        long totalBytesRead = 0;
                        int bytesRead;
                        while (totalBytesRead < dataLen)
                        {
                            int bytesToRead = (int)Math.Min(81920L, dataLen - totalBytesRead);
                            bytesRead = br.Read(fileInfo.data, (int)totalBytesRead, bytesToRead);
                            if (bytesRead == 0) break;
                            totalBytesRead += bytesRead;
                        }
                        if (totalBytesRead == dataLen) return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileData(byte[])] Exception: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 读取文件为字节数组，自动释放资源。
        /// </summary>
        public static byte[] ReadFile(string fileName)
        {
            if (!File.Exists(fileName)) return null;
            try
            {
                using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader binaryReader = new BinaryReader(fileStream))
                {
                    long length = fileStream.Length;
                    if (length > int.MaxValue) throw new IOException("File too large");
                    byte[] bytes = new byte[length];
                    binaryReader.Read(bytes, 0, bytes.Length);
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadFile] Exception: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 尝试从字节数组读取一个UInt32。
        /// </summary>
        private static bool TryReadUInt32(byte[] data, ref int startIndex, out int value)
        {
            if (startIndex + 4 > data.Length)
            {
                value = 0;
                return false;
            }
            value = (int)BitConverter.ToUInt32(data, startIndex);
            startIndex += 4;
            return true;
        }

        /// <summary>
        /// 尝试从字节数组读取一个Single。
        /// </summary>
        private static bool TryReadSingle(byte[] data, ref int startIndex, out float value)
        {
            if (startIndex + sizeof(int) > data.Length)
            {
                value = 0;
                return false;
            }
            value = BitConverter.ToSingle(data, startIndex);
            startIndex += sizeof(int);
            return true;
        }

        /// <summary>
        /// 读取CVCIE或CVRAW文件（自动判断格式）。
        /// </summary>
        public static bool Read(string filePath, out CVCIEFile fileInfo)
        {
            int index = ReadCIEFileHeader(filePath, out fileInfo);
            if (index > 0)
                return ReadCIEFileData(filePath, ref fileInfo, index);
            return false;
        }

        /// <summary>
        /// 读取CVCIE或CVRAW文件（自动判断格式，字节数组）。
        /// </summary>
        public static bool Read(byte[] fileData, out CVCIEFile fileInfo)
        {
            int index = ReadCIEFileHeader(fileData, out fileInfo);
            if (index > 0)
                return ReadCIEFileData(fileData, ref fileInfo, index);
            return false;
        }

        /// <summary>
        /// 按通道类型打开本地文件（自动判断扩展名）。
        /// </summary>
        public static CVCIEFile OpenLocalFileChannel(string fileName, CVImageChannelType channelType)
        {
            string ext = Path.GetExtension(fileName)?.ToLower(CultureInfo.CurrentCulture);
            CVType fileExtType = ext.Contains(".cvraw") ? CVType.Raw : ext.Contains(".cvsrc") ? CVType.Src : CVType.CIE;
            return OpenLocalFileChannel(fileName, fileExtType, channelType);
        }

        /// <summary>
        /// 按通道类型和文件类型打开本地文件。
        /// </summary>
        public static CVCIEFile OpenLocalFileChannel(string fileName, CVType extType, CVImageChannelType channelType)
        {
            if (channelType == CVImageChannelType.SRC)
            {
                if (extType == CVType.Raw && ReadCVRaw(fileName, out var meta))
                {
                    return meta;
                }
                if (extType == CVType.CIE && ReadCVCIE(fileName, out var meta2))
                {
                    return meta2;
                }
            }
            int channel = -1;
            CVCIEFile data = default(CVCIEFile);
            switch (channelType)
            {
                case CVImageChannelType.CIE_XYZ_X:
                    channel = 0;
                    break;
                case CVImageChannelType.CIE_XYZ_Y:
                    channel = 1;
                    break;
                case CVImageChannelType.CIE_XYZ_Z:
                    channel = 2;
                    break;
            }
            if (channel >= 0 && extType == CVType.CIE)
            {
                ReadCVCIEXYZ(fileName, channel, out data);
            }
            return data;
        }

        /// <summary>
        /// 按文件类型自动打开本地CV文件。
        /// </summary>
        public static CVCIEFile OpenLocalCVFile(string fileName)
        {
            CVType extType = CVType.Src;
            if (Path.GetExtension(fileName).Contains("cvraw"))
            {
                extType = CVType.Raw;
            }
            else if (Path.GetExtension(fileName).Contains("cvcie"))
            {
                extType = CVType.CIE;
            }
            return OpenLocalCVFile(fileName, extType);
        }

        /// <summary>
        /// 按指定类型打开本地CV文件。
        /// </summary>
        public static CVCIEFile OpenLocalCVFile(string fileName, CVType extType)
        {
            CVCIEFile fileInfo = new CVCIEFile();
            if (extType == CVType.CIE)
            {
                CVFileUtil.ReadCVCIE(fileName, out fileInfo);
            }
            else if (extType == CVType.Raw)
            {
                CVFileUtil.ReadCVRaw(fileName, out fileInfo);
            }
            else if (extType == CVType.Src)
            {
                CVFileUtil.ReadCVRaw(fileName, out fileInfo);
            }
            return fileInfo;
        }

        /// <summary>
        /// 读取CVRAW文件（兼容接口）。
        /// </summary>
        public static bool ReadCVRaw(string fileName, out CVCIEFile fileInfo) => Read(fileName, out fileInfo);

        /// <summary>
        /// 读取CVCIE文件（兼容接口）。
        /// </summary>
        public static bool ReadCVCIE(string fileName, out CVCIEFile fileInfo)
        {
            int startIndex = CVFileUtil.ReadCIEFileHeader(fileName, out fileInfo);
            if (startIndex < 0) return false;
            fileInfo.FilePath = fileName;
            return ReadCIEFileData(fileName, ref fileInfo, startIndex);
        }

        /// <summary>
        /// 按通道读取CVCIE文件的指定通道数据。
        /// </summary>
        public static int ReadCVCIEXYZ(string fileName, int channel, out CVCIEFile fileOut)
        {
            int index = ReadCIEFileHeader(fileName, out fileOut);
            if (index < 0) return -1;
            ReadCIEFileData(fileName, ref fileOut, index);
            if (fileOut.channels > 1)
            {
                fileOut.FileExtType = CVType.Raw;
                fileOut.channels = 1;
                int len = fileOut.cols * fileOut.rows * fileOut.bpp / 8;
                try
                {
                    byte[] data = new byte[len];
                    Buffer.BlockCopy(fileOut.data, channel * len, data, 0, len);
                    fileOut.data = data;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ReadCVCIEXYZ] Exception: {ex}");
                    return -2;
                }
                return 0;
            }
            return -2;
        }
    }
}
