using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace ColorVision.FileIO
{
    /// <summary>
    /// Image channel type enumeration for ColorVision files.
    /// </summary>
    public enum CVImageChannelType
    {
        /// <summary>Source image (all Channels).</summary>
        SRC = 0,
        
        /// <summary>RGB Red channel.</summary>
        RgbR = 1,
        /// <summary>RGB Green channel.</summary>
        RgbG = 2,
        /// <summary>RGB Blue channel.</summary>
        RgbB = 3,
        
        /// <summary>CIE XYZ X component.</summary>
        CieXyzX = 10,
        /// <summary>CIE XYZ Y component (luminance).</summary>
        CieXyzY = 11,
        /// <summary>CIE XYZ Z component.</summary>
        CieXyzZ = 12,
        
        /// <summary>CIE Lv (luminance value).</summary>
        CieLv = 13,
        /// <summary>CIE x chromaticity coordinate.</summary>
        CieX = 14,
        /// <summary>CIE y chromaticity coordinate.</summary>
        CieY = 15,
        /// <summary>CIE u' chromaticity coordinate.</summary>
        CieU = 16,
        /// <summary>CIE v' chromaticity coordinate.</summary>
        CieV = 17,
    }

    /// <summary>
    /// CVCIE文件操作工具类，支持CVCIE格式的读写、通道提取等。
    /// </summary>
    public static class CVFileUtil
    {
        private const string MagicHeader = "CVCIE";
        private const int HeaderSize = 5;
        private const int MinimumFileSize = HeaderSize + 4; // Minimum file size to contain the header and Version
        private static readonly Encoding Encoding1 = Encoding.GetEncoding("GBK");

        static CVFileUtil()
        {
#if NETCOREAPP
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
        }
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
        public static bool IsCVCIEFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < HeaderSize) return false;
                    string fileHeader = new string(br.ReadChars(HeaderSize));
                    if (fileHeader == MagicHeader)
                    {
                        CVCIEFile cVCIEFile = new CVCIEFile();
                        int index = ReadCIEFileHeader(filePath ,out CVCIEFile cvcie);
                        if (index > 0)
                        {
                            return cvcie.FileExtType == CVType.CIE;
                        }
                    }
                    return  false;
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
            cvcie = new CVCIEFile();
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
                    uint ver = (cvcie.Version = br.ReadUInt32());
                    if (ver ==1 ||ver == 2)
                    {
                        int fileNameLen = br.ReadInt32();
                        if (fileNameLen > 0 && fs.Position + fileNameLen <= fs.Length)
                            cvcie.SrcFileName = Encoding1.GetString(br.ReadBytes(fileNameLen));
                        cvcie.Gain = br.ReadSingle();
                        cvcie.Channels = (int)br.ReadUInt32();
                        if (fs.Position + cvcie.Channels * 4 > fs.Length) return -1;
                        cvcie.Exp = new float[cvcie.Channels];
                        for (int i = 0; i < cvcie.Channels; i++)
                            cvcie.Exp[i] = br.ReadSingle();
                        cvcie.Rows = (int)br.ReadUInt32();
                        cvcie.Cols = (int)br.ReadUInt32();
                        cvcie.Bpp = (int)br.ReadUInt32();
                        cvcie.FilePath = filePath;
                        return (int)fs.Position;
                    }
                    else if (ver == 3)
                    {
                        int fileNameLen = br.ReadInt32();
                        if (fileNameLen > 0 && fs.Position + fileNameLen <= fs.Length)
                            cvcie.SrcFileName = Encoding1.GetString(br.ReadBytes(fileNameLen));
                        cvcie.NDPort = br.ReadInt32();
                        cvcie.Gain = br.ReadSingle();
                        cvcie.Channels = br.ReadInt32();
                        if (fs.Position + cvcie.Channels * 4 > fs.Length) return -1;
                        cvcie.Exp = new float[cvcie.Channels];
                        for (int i = 0; i < cvcie.Channels; i++)
                            cvcie.Exp[i] = br.ReadSingle();
                        cvcie.Rows = br.ReadInt32();
                        cvcie.Cols = br.ReadInt32();
                        cvcie.Bpp = br.ReadInt32();
                        cvcie.FilePath = filePath;
                        return (int)fs.Position;
                    }
                    else
                    {
                        Debug.WriteLine($"unknowVersion");
                        return -1;
                    }


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
            cvcie = new CVCIEFile();
            if (fileData == null || fileData.Length < 9) return -1;
            try
            {
                string fileHeader = Encoding.ASCII.GetString(fileData, 0, HeaderSize);
                if (fileHeader != MagicHeader) return -1;
                int startIndex = HeaderSize;
                uint version = (cvcie.Version = BitConverter.ToUInt32(fileData, startIndex));
                if (version != 1 && version != 2) return -1;
                startIndex += 4;
                int fileNameLength = BitConverter.ToInt32(fileData, startIndex);
                startIndex += 4;
                if (fileNameLength < 0 || startIndex + fileNameLength > fileData.Length) return -1;
                cvcie.SrcFileName = Encoding1.GetString(fileData, startIndex, fileNameLength);
                startIndex += fileNameLength;
                float gainValue;
                if (!TryReadSingle(fileData, ref startIndex, out gainValue)) return -1;
                cvcie.Gain = gainValue;
                int channelsValue;
                if (!TryReadUInt32(fileData, ref startIndex, out channelsValue)) return -1;
                cvcie.Channels = channelsValue;
                if (startIndex + cvcie.Channels * 4 > fileData.Length) return -1;
                cvcie.Exp = new float[cvcie.Channels];
                for (int i = 0; i < cvcie.Channels; i++)
                {
                    cvcie.Exp[i] = BitConverter.ToSingle(fileData, startIndex);
                    startIndex += 4;
                }
                int rowsValue, colsValue, bppValue;
                if (!TryReadUInt32(fileData, ref startIndex, out rowsValue)) return -1;
                cvcie.Rows = rowsValue;
                if (!TryReadUInt32(fileData, ref startIndex, out colsValue)) return -1;
                cvcie.Cols = colsValue;
                if (!TryReadUInt32(fileData, ref startIndex, out bppValue)) return -1;
                cvcie.Bpp = bppValue;
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
                    uint version = fileInfo.Version;
                    long dataLen = (version != 2) ? br.ReadInt32() : br.ReadInt64();
                    if (dataLen > 0 && fs.Position + dataLen <= fs.Length)
                    {
                        try
                        {
                            fileInfo.Data = new byte[dataLen];
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
                            bytesRead = br.Read(fileInfo.Data, (int)totalBytesRead, bytesToRead);
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
                    uint version = fileInfo.Version;
                    long dataLen = (version != 2) ? br.ReadInt32() : br.ReadInt64();
                    if (dataLen > 0 && ms.Position + dataLen <= ms.Length)
                    {
                        try
                        {
                            fileInfo.Data = new byte[dataLen];
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
                            bytesRead = br.Read(fileInfo.Data, (int)totalBytesRead, bytesToRead);
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
            CVCIEFile data = new CVCIEFile();
            switch (channelType)
            {
                case CVImageChannelType.CieXyzX:
                    channel = 0;
                    break;
                case CVImageChannelType.CieXyzY:
                    channel = 1;
                    break;
                case CVImageChannelType.CieXyzZ:
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

            if (!string.IsNullOrEmpty(fileInfo.SrcFileName))
            {
                if (CVFileUtil.ReadCVCIESrc(fileName, out CVCIEFile fileInf))
                {
                    fileInf.SrcFileName = fileInfo.SrcFileName;
                    fileInfo = fileInf;
                    fileInfo.FilePath = fileName;
                    return true;
                }
            }

            return ReadCIEFileData(fileName, ref fileInfo, startIndex);
        }


        public static bool ReadCVCIESrc(string FileName, out CVCIEFile fileOut)
        {
            fileOut = new CVCIEFile();
            int index = ReadCIEFileHeader(FileName, out CVCIEFile cvcie);
            if (index < 0) return false;
            if (!File.Exists(cvcie.SrcFileName))
                cvcie.SrcFileName = Path.Combine(Path.GetDirectoryName(FileName) ?? string.Empty, cvcie.SrcFileName);

            if (File.Exists(cvcie.SrcFileName))
            {
                if (IsCIEFile(cvcie.SrcFileName))
                {
                    fileOut.FileExtType = CVType.Raw;
                    return Read(cvcie.SrcFileName, out fileOut);
                }
                else
                {
                    if (cvcie.SrcFileName != null)
                    {
                        fileOut.Data = ReadFile(cvcie.SrcFileName);
                        fileOut.FileExtType = CVType.Tif;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 按通道读取CVCIE文件的指定通道数据。
        /// </summary>
        public static int ReadCVCIEXYZ(string fileName, int channel, out CVCIEFile fileOut)
        {
            int index = ReadCIEFileHeader(fileName, out fileOut);
            if (index < 0) return -1;
            ReadCIEFileData(fileName, ref fileOut, index);
            if (fileOut.Channels > 1)
            {
                fileOut.FileExtType = CVType.Raw;
                fileOut.Channels = 1;
                int len = fileOut.Cols * fileOut.Rows * fileOut.Bpp / 8;
                try
                {
                    byte[] data = new byte[len];
                    Buffer.BlockCopy(fileOut.Data, channel * len, data, 0, len);
                    fileOut.Data = data;
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

        #region Write Methods

        /// <summary>
        /// Writes a CVCIE file with the provided file information.
        /// </summary>
        /// <param name="filePath">The path where the file should be written.</param>
        /// <param name="fileInfo">The CVCIEFile structure containing the Data to write.</param>
        /// <returns>True if the file was written successfully; otherwise, false.</returns>
        public static bool WriteCIEFile(string filePath, CVCIEFile fileInfo)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            if (fileInfo == null) return false;

            try
            {
                // Get encoding, fallback to UTF8 if GBK is not available
                Encoding encoding;
                try
                {
                    encoding = Encoding.GetEncoding("GBK");
                }
                catch
                {
                    encoding = Encoding.UTF8;
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    // Write magic header
                    bw.Write(MagicHeader.ToCharArray());

                    // Write Version
                    bw.Write(fileInfo.Version);

                    // Write source file name
                    string srcFileName = fileInfo.SrcFileName ?? string.Empty;
                    byte[] srcFileNameBytes = encoding.GetBytes(srcFileName);
                    bw.Write(srcFileNameBytes.Length);
                    if (srcFileNameBytes.Length > 0)
                    {
                        bw.Write(srcFileNameBytes);
                    }

                    // Write Gain
                    bw.Write(fileInfo.Gain);

                    // Write Channels and exposure values
                    bw.Write((uint)fileInfo.Channels);
                    if (fileInfo.Exp != null && fileInfo.Exp.Length > 0)
                    {
                        for (int i = 0; i < Math.Min(fileInfo.Channels, fileInfo.Exp.Length); i++)
                        {
                            bw.Write(fileInfo.Exp[i]);
                        }
                        // Fill remaining Channels with 0 if Exp array is shorter
                        for (int i = fileInfo.Exp.Length; i < fileInfo.Channels; i++)
                        {
                            bw.Write(0f);
                        }
                    }
                    else
                    {
                        // No exposure Data, write zeros
                        for (int i = 0; i < fileInfo.Channels; i++)
                        {
                            bw.Write(0f);
                        }
                    }

                    // Write image dimensions and bit depth
                    bw.Write((uint)fileInfo.Rows);
                    bw.Write((uint)fileInfo.Cols);
                    bw.Write((uint)fileInfo.Bpp);

                    // Write Data
                    if (fileInfo.Data != null && fileInfo.Data.Length > 0)
                    {
                        if (fileInfo.Version == 2)
                        {
                            bw.Write((long)fileInfo.Data.Length);
                        }
                        else
                        {
                            bw.Write((int)fileInfo.Data.Length);
                        }
                        bw.Write(fileInfo.Data);
                    }
                    else
                    {
                        // No Data, write zero length
                        if (fileInfo.Version == 2)
                        {
                            bw.Write(0L);
                        }
                        else
                        {
                            bw.Write(0);
                        }
                    }

                    bw.Flush();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WriteCIEFile] Exception: {ex.Message}");
                Debug.WriteLine($"[WriteCIEFile] StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Writes a CVCIE file to a byte array.
        /// </summary>
        /// <param name="fileInfo">The CVCIEFile structure containing the Data to write.</param>
        /// <param name="fileData">The output byte array containing the file Data.</param>
        /// <returns>True if the Data was written successfully; otherwise, false.</returns>
        public static bool WriteCIEFile(CVCIEFile fileInfo, out byte[] fileData)
        {
            fileData = null;
            if (fileInfo == null) return false;

            try
            {
                // Get encoding, fallback to UTF8 if GBK is not available
                Encoding encoding;
                try
                {
                    encoding = Encoding.GetEncoding("GBK");
                }
                catch
                {
                    encoding = Encoding.UTF8;
                }

                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    // Write magic header
                    bw.Write(MagicHeader.ToCharArray());

                    // Write Version
                    bw.Write(fileInfo.Version);

                    // Write source file name
                    string srcFileName = fileInfo.SrcFileName ?? string.Empty;
                    byte[] srcFileNameBytes = encoding.GetBytes(srcFileName);
                    bw.Write(srcFileNameBytes.Length);
                    if (srcFileNameBytes.Length > 0)
                    {
                        bw.Write(srcFileNameBytes);
                    }

                    // Write Gain
                    bw.Write(fileInfo.Gain);

                    // Write Channels and exposure values
                    bw.Write((uint)fileInfo.Channels);
                    if (fileInfo.Exp != null && fileInfo.Exp.Length > 0)
                    {
                        for (int i = 0; i < Math.Min(fileInfo.Channels, fileInfo.Exp.Length); i++)
                        {
                            bw.Write(fileInfo.Exp[i]);
                        }
                        for (int i = fileInfo.Exp.Length; i < fileInfo.Channels; i++)
                        {
                            bw.Write(0f);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < fileInfo.Channels; i++)
                        {
                            bw.Write(0f);
                        }
                    }

                    // Write image dimensions and bit depth
                    bw.Write((uint)fileInfo.Rows);
                    bw.Write((uint)fileInfo.Cols);
                    bw.Write((uint)fileInfo.Bpp);

                    // Write Data
                    if (fileInfo.Data != null && fileInfo.Data.Length > 0)
                    {
                        if (fileInfo.Version == 2)
                        {
                            bw.Write((long)fileInfo.Data.Length);
                        }
                        else
                        {
                            bw.Write((int)fileInfo.Data.Length);
                        }
                        bw.Write(fileInfo.Data);
                    }
                    else
                    {
                        if (fileInfo.Version == 2)
                        {
                            bw.Write(0L);
                        }
                        else
                        {
                            bw.Write(0);
                        }
                    }

                    bw.Flush();
                    fileData = ms.ToArray();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WriteCIEFile(byte[])] Exception: {ex.Message}");
                Debug.WriteLine($"[WriteCIEFile(byte[])] StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Writes a CVRAW file (convenience wrapper for WriteCIEFile).
        /// </summary>
        /// <param name="filePath">The path where the file should be written.</param>
        /// <param name="fileInfo">The CVCIEFile structure containing the Data to write.</param>
        /// <returns>True if the file was written successfully; otherwise, false.</returns>
        public static bool WriteCVRaw(string filePath, CVCIEFile fileInfo)
        {
            return WriteCIEFile(filePath, fileInfo);
        }

        /// <summary>
        /// Writes a CVCIE file (convenience wrapper for WriteCIEFile).
        /// </summary>
        /// <param name="filePath">The path where the file should be written.</param>
        /// <param name="fileInfo">The CVCIEFile structure containing the Data to write.</param>
        /// <returns>True if the file was written successfully; otherwise, false.</returns>
        public static bool WriteCVCIE(string filePath, CVCIEFile fileInfo)
        {
            return WriteCIEFile(filePath, fileInfo);
        }

        /// <summary>
        /// Writes image Data to a file in CVCIE format with specified parameters.
        /// </summary>
        /// <param name="filePath">The path where the file should be written.</param>
        /// <param name="data">The raw image Data bytes.</param>
        /// <param name="rows">Number of Rows (height).</param>
        /// <param name="cols">Number of columns (width).</param>
        /// <param name="bpp">Bits per pixel.</param>
        /// <param name="channels">Number of Channels.</param>
        /// <param name="gain">Gain value (default 1.0).</param>
        /// <param name="exp">Exposure values array (optional).</param>
        /// <param name="srcFileName">Source file name (optional).</param>
        /// <param name="version">File format Version (default 1).</param>
        /// <returns>True if the file was written successfully; otherwise, false.</returns>
        public static bool WriteCIEFile(string filePath, byte[] data, int rows, int cols, int bpp, int channels, 
            float gain = 1.0f, float[] exp = null, string srcFileName = null, uint version = 1)
        {
            if (data == null || data.Length == 0) return false;

            CVCIEFile fileInfo = new CVCIEFile
            {
                Version = version,
                FileExtType = CVType.CIE,
                Rows = rows,
                Cols = cols,
                Bpp = bpp,
                Channels = channels,
                Gain = gain,
                Exp = exp ?? new float[channels],
                SrcFileName = srcFileName,
                Data = data
            };

            return WriteCIEFile(filePath, fileInfo);
        }

        #endregion

 
    }
}
