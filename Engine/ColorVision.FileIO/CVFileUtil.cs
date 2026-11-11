using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;

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
        private static readonly Encoding Encoding1;

        static CVFileUtil()
        {
#if NETCOREAPP
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
            Encoding1 = Encoding.GetEncoding("GBK");
        }
        /// <summary>
        /// 判断文件是否为CVCIE格式。
        /// </summary>
        public static bool IsCIEFile(string filePath)
        {
            try
            {
                using (Stream fs = CVFileReadCache.OpenRead(filePath, populateCache: false))
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
            try
            {
                using (Stream fs = CVFileReadCache.OpenRead(filePath, populateCache: false))
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
            try
            {
                using (Stream fs = CVFileReadCache.OpenRead(filePath, populateCache: false))
                {
                    int index = ReadCIEFileHeader(fs, out cvcie);
                    cvcie.FileExtType = filePath.Contains(".cvraw") ? CVType.Raw : filePath.Contains(".cvsrc") ? CVType.Src : CVType.CIE;
                    if (index > 0) cvcie.FilePath = filePath;
                    return index;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileHeader] Exception: {ex}");
                return -1;
            }
        }

        /// <summary>读取文件头并保留流，由调用方继续读取同一文件快照的像素。流必须可定位；返回长度前缀的偏移。</summary>
        public static int ReadCIEFileHeader(Stream fs, out CVCIEFile cvcie)
            => ReadCIEFileHeader(fs, out cvcie, int.MaxValue);

        /// <summary>Reads a seekable stream header with a caller-specified channel limit before allocating exposure values.</summary>
        public static int ReadCIEFileHeader(Stream fs, out CVCIEFile cvcie, int maximumChannels)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maximumChannels);
            cvcie = new CVCIEFile();
            try
            {
                fs.Position = 0;
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true))
                {
                    if (fs.Length < 9) return -1;
                    string fileHeader = new string(br.ReadChars(HeaderSize));
                    if (fileHeader != MagicHeader) return -1;
                    uint ver = (cvcie.Version = br.ReadUInt32());
                    if (ver ==1 ||ver == 2)
                    {
                        int fileNameLen = br.ReadInt32();
                        if (fileNameLen < 0 || fileNameLen > fs.Length - fs.Position) return -1;
                        if (fileNameLen > 0)
                            cvcie.SrcFileName = Encoding1.GetString(br.ReadBytes(fileNameLen));
                        cvcie.Gain = br.ReadSingle();
                        cvcie.Channels = (int)br.ReadUInt32();
                        if (!HasChannelMetadata(fs.Length - fs.Position, cvcie.Channels, maximumChannels)) return -1;
                        cvcie.Exp = new float[cvcie.Channels];
                        for (int i = 0; i < cvcie.Channels; i++)
                            cvcie.Exp[i] = br.ReadSingle();
                        cvcie.Cols = (int)br.ReadUInt32();
                        cvcie.Rows = (int)br.ReadUInt32();
                        cvcie.Bpp = (int)br.ReadUInt32();
                        return (int)fs.Position;
                    }
                    else if (ver == 3)
                    {
                        int fileNameLen = br.ReadInt32();
                        if (fileNameLen < 0 || fileNameLen > fs.Length - fs.Position) return -1;
                        if (fileNameLen > 0)
                            cvcie.SrcFileName = Encoding1.GetString(br.ReadBytes(fileNameLen));
                        cvcie.NDPort = br.ReadInt32();
                        cvcie.Gain = br.ReadSingle();
                        cvcie.Channels = br.ReadInt32();
                        if (!HasChannelMetadata(fs.Length - fs.Position, cvcie.Channels, maximumChannels)) return -1;
                        cvcie.Exp = new float[cvcie.Channels];
                        for (int i = 0; i < cvcie.Channels; i++)
                            cvcie.Exp[i] = br.ReadSingle();
                        cvcie.Cols = br.ReadInt32();
                        cvcie.Rows = br.ReadInt32();
                        cvcie.Bpp = br.ReadInt32();
                        return (int)fs.Position;
                    }
                }
                return -1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileHeader] Exception: {ex}");
                return -1;
            }
        }

        private static bool HasChannelMetadata(long remainingBytes, int channels, int maximumChannels) =>
            channels >= 0 && channels <= maximumChannels && (long)channels * sizeof(float) + 3 * sizeof(int) <= remainingBytes;

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
                startIndex += 4;
                if (version == 1 || version == 2)
                {
                    int fileNameLength = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    if (fileNameLength < 0 || fileNameLength > fileData.Length - startIndex) return -1;
                    cvcie.SrcFileName = Encoding1.GetString(fileData, startIndex, fileNameLength);
                    startIndex += fileNameLength;
                    float gainValue;
                    if (!TryReadSingle(fileData, ref startIndex, out gainValue)) return -1;
                    cvcie.Gain = gainValue;
                    int channelsValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out channelsValue)) return -1;
                    cvcie.Channels = channelsValue;
                    if (!HasChannelMetadata(fileData.Length - startIndex, cvcie.Channels, int.MaxValue)) return -1;
                    cvcie.Exp = new float[cvcie.Channels];
                    for (int i = 0; i < cvcie.Channels; i++)
                    {
                        cvcie.Exp[i] = BitConverter.ToSingle(fileData, startIndex);
                        startIndex += 4;
                    }
                    int rowsValue, colsValue, bppValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out rowsValue)) return -1;
                    cvcie.Cols = rowsValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out colsValue)) return -1;
                    cvcie.Rows = colsValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out bppValue)) return -1;
                    cvcie.Bpp = bppValue;
                    return startIndex;
                }
                else if (version == 3)
                {
                    int fileNameLength = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    if (fileNameLength < 0 || fileNameLength > fileData.Length - startIndex) return -1;
                    cvcie.SrcFileName = Encoding1.GetString(fileData, startIndex, fileNameLength);
                    startIndex += fileNameLength;
                    int ndPortValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out ndPortValue)) return -1;
                    cvcie.NDPort = ndPortValue;
                    float gainValue;
                    if (!TryReadSingle(fileData, ref startIndex, out gainValue)) return -1;
                    cvcie.Gain = gainValue;
                    int channelsValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out channelsValue)) return -1;
                    cvcie.Channels = channelsValue;
                    if (!HasChannelMetadata(fileData.Length - startIndex, cvcie.Channels, int.MaxValue)) return -1;
                    cvcie.Exp = new float[cvcie.Channels];
                    for (int i = 0; i < cvcie.Channels; i++)
                    {
                        cvcie.Exp[i] = BitConverter.ToSingle(fileData, startIndex);
                        startIndex += 4;
                    }
                    int colsValue, rowsValue, bppValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out colsValue)) return -1;
                    cvcie.Cols = colsValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out rowsValue)) return -1;
                    cvcie.Rows = rowsValue;
                    if (!TryReadUInt32(fileData, ref startIndex, out bppValue)) return -1;
                    cvcie.Bpp = bppValue;
                    return startIndex;
                }
                return -1;
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
                using Stream stream = CVFileReadCache.OpenRead(filePath);
                return ReadCIEFileData(stream, fileInfo, dataStartIndex);
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
            if (fileData == null) return false;
            using var stream = new MemoryStream(fileData, writable: false);
            return ReadCIEFileData(stream, fileInfo, dataStartIndex);
        }

        /// <summary>
        /// Reads the payload or one planar channel from the same seekable stream as its header.
        /// Leaves the stream open and publishes data only after the requested bytes are read completely.
        /// </summary>
        public static bool ReadCIEFileData(Stream stream, CVCIEFile fileInfo, int dataStartIndex, int? channelIndex = null)
        {
            try
            {
                long channelBytes = 0;
                if (channelIndex is int channel && !TryGetChannelByteCount(fileInfo, channel, out channelBytes)) return false;
                return ReadPayload(stream, fileInfo, dataStartIndex, channelIndex.GetValueOrDefault(), channelBytes,
                    requireExactPayload: false, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileData(Stream)] Exception: {ex}");
                return false;
            }
        }

        /// <summary>Reads exactly the image layout's bytes after checking the declared payload covers that layout.</summary>
        public static bool ReadCIEFilePixels(Stream stream, CVCIEFile fileInfo, int dataStartIndex)
            => ReadCIEFilePixels(stream, fileInfo, dataStartIndex, requireExactPayload: false, CancellationToken.None);

        /// <summary>Reads a complete pixel layout, with optional strict length validation and cooperative cancellation.</summary>
        public static bool ReadCIEFilePixels(Stream stream, CVCIEFile fileInfo, int dataStartIndex, bool requireExactPayload,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetChannelByteCount(fileInfo, 0, out long channelBytes)) return false;
                return ReadPayload(stream, fileInfo, dataStartIndex, 0, channelBytes, requireExactPayload,
                    cancellationToken, readWholeLayout: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFilePixels] Exception: {ex}");
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
        /// Reads one embedded channel directly from a CVCIE/CVRAW file.
        /// Header metadata is preserved in <paramref name="fileInfo"/> while
        /// <see cref="CVCIEFile.Data"/> contains only the requested channel.
        /// Unlike <see cref="Read(string, out CVCIEFile)"/>, this method does not
        /// allocate a complete multi-channel result. A cache miss may warm the shared file slot.
        /// </summary>
        /// <param name="filePath">Path to the CVCIE/CVRAW file.</param>
        /// <param name="channelIndex">Zero-based embedded channel index.</param>
        /// <param name="fileInfo">Parsed header metadata and requested channel data.</param>
        /// <param name="cancellationToken">Token checked while reading large channels.</param>
        /// <returns><see langword="true"/> when the header and complete channel were read.</returns>
        public static bool ReadCIEFileChannel(
            string filePath,
            int channelIndex,
            out CVCIEFile fileInfo,
            CancellationToken cancellationToken = default)
        {
            int dataStartIndex = ReadCIEFileHeader(filePath, out fileInfo);
            if (dataStartIndex < 0)
            {
                return false;
            }

            return ReadCIEFileChannelData(filePath, channelIndex, fileInfo, dataStartIndex, requireExactPayload: false, cancellationToken);
        }

        private static bool ReadCIEFileChannelData(string filePath, int channelIndex, CVCIEFile fileInfo,
            int dataStartIndex, bool requireExactPayload, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetChannelByteCount(fileInfo, channelIndex, out long channelBytes)) return false;
                using Stream stream = CVFileReadCache.OpenRead(filePath);
                return ReadPayload(stream, fileInfo, dataStartIndex, channelIndex, channelBytes, requireExactPayload, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                fileInfo.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReadCIEFileChannel] Exception: {ex}");
                return false;
            }
        }

        private static bool TryGetChannelByteCount(CVCIEFile fileInfo, int channelIndex, out long channelBytes)
        {
            channelBytes = 0;
            if (channelIndex < 0 || channelIndex >= fileInfo.Channels || fileInfo.Rows <= 0 || fileInfo.Cols <= 0
                || fileInfo.Bpp <= 0 || fileInfo.Bpp % 8 != 0)
                return false;

            channelBytes = checked((long)fileInfo.Rows * fileInfo.Cols * (fileInfo.Bpp / 8));
            return channelBytes <= Array.MaxLength;
        }

        private static bool ReadPayload(Stream stream, CVCIEFile fileInfo, int dataStartIndex, int channelIndex, long channelBytes,
            bool requireExactPayload, CancellationToken cancellationToken, bool readWholeLayout = false)
        {
            int prefixSize = fileInfo.Version == 2 ? sizeof(long) : sizeof(int);
            if (dataStartIndex < 0 || stream.Length - dataStartIndex < prefixSize) return false;

            stream.Position = dataStartIndex;
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            long payloadBytes = fileInfo.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
            cancellationToken.ThrowIfCancellationRequested();
            long payloadStart = stream.Position;
            long requiredBytes = channelBytes * fileInfo.Channels;
            if (payloadBytes <= 0 || payloadBytes < requiredBytes || payloadBytes > stream.Length - payloadStart
                || (requireExactPayload && payloadBytes != requiredBytes))
                return false;

            long count = readWholeLayout ? requiredBytes : channelBytes == 0 ? payloadBytes : channelBytes;
            if (count > Array.MaxLength) return false;
            if (channelIndex != 0) stream.Position = checked(payloadStart + channelBytes * channelIndex);

            byte[] data = GC.AllocateUninitializedArray<byte>((int)count);
            for (int offset = 0; offset < data.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int bytesRead = stream.Read(data, offset, Math.Min(81920, data.Length - offset));
                if (bytesRead == 0) return false;
                offset += bytesRead;
            }
            cancellationToken.ThrowIfCancellationRequested();
            fileInfo.Data = data;
            return true;
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
            => OpenLocalCVFile(fileName, GetLocalFileType(fileName));

        /// <summary>按本地打开规则选择源图。内嵌 XYZ 用于灰度显示时只读取首平面。</summary>
        public static CVCIEFile OpenLocalCVFileForDisplay(string fileName)
        {
            ReadCVCIECore(fileName, out CVCIEFile fileInfo,
                followAssociatedSource: GetLocalFileType(fileName) == CVType.CIE, firstEmbeddedPlaneOnly: true);
            return fileInfo;
        }

        private static CVType GetLocalFileType(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            if (extension.Contains("cvraw")) return CVType.Raw;
            if (extension.Contains("cvcie")) return CVType.CIE;
            return CVType.Src;
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
            => ReadCVCIECore(fileName, out fileInfo, followAssociatedSource: true, firstEmbeddedPlaneOnly: false);

        private static bool ReadCVCIECore(string fileName, out CVCIEFile fileInfo, bool followAssociatedSource, bool firstEmbeddedPlaneOnly)
        {
            int startIndex = CVFileUtil.ReadCIEFileHeader(fileName, out fileInfo);
            if (startIndex < 0) return false;
            fileInfo.FilePath = fileName;

            if (followAssociatedSource && !string.IsNullOrEmpty(fileInfo.SrcFileName))
            {
                if (CVFileUtil.ReadCVCIESrc(fileName, out CVCIEFile fileInf))
                {
                    fileInf.SrcFileName = fileInfo.SrcFileName;
                    fileInfo = fileInf;
                    fileInfo.FilePath = fileName;
                    return true;
                }
            }

            if (firstEmbeddedPlaneOnly && fileInfo.FileExtType == CVType.CIE && fileInfo.Channels == 3)
            {
                if (!ReadCIEFileChannelData(fileName, 0, fileInfo, startIndex, requireExactPayload: true, CancellationToken.None))
                    return false;
                fileInfo.Channels = 1;
                return true;
            }
            return ReadCIEFileData(fileName, ref fileInfo, startIndex);
        }


        public static bool ReadCVCIESrc(string FileName, out CVCIEFile fileOut)
        {
            fileOut = new CVCIEFile();
            int index = ReadCIEFileHeader(FileName, out CVCIEFile cvcie);
            using (cvcie)
            {
                if (index < 0 || string.IsNullOrEmpty(cvcie.SrcFileName)) return false;
                string sourcePath = Path.Combine(Path.GetDirectoryName(FileName) ?? string.Empty, cvcie.SrcFileName);
                if (!File.Exists(sourcePath)) sourcePath = cvcie.SrcFileName;
                if (!File.Exists(sourcePath)) return false;
                if (IsCIEFile(sourcePath)) return Read(sourcePath, out fileOut);

                fileOut.Data = ReadFile(sourcePath);
                fileOut.FileExtType = CVType.Tif;
                return true;
            }
        }

        /// <summary>
        /// 按通道读取CVCIE文件的指定通道数据。
        /// </summary>
        public static int ReadCVCIEXYZ(string fileName, int channel, out CVCIEFile fileOut)
        {
            int index = ReadCIEFileHeader(fileName, out fileOut);
            if (index < 0) return -1;
            if (fileOut.Channels <= 1) return -2;

            // The former Cols * Rows * Bpp / 8 expression overflowed before division
            // for large images, returning a short array with full-size metadata.
            // Reuse the checked byte-count and complete-channel read boundary.
            fileOut.Dispose();
            if (!ReadCIEFileChannel(fileName, channel, out fileOut)) return -2;
            fileOut.FileExtType = CVType.Raw;
            fileOut.Channels = 1;
            return 0;
        }

        #region Write Methods

        /// <summary>
        /// Writes a CVCIE file with the provided file information.
        /// </summary>
        /// <param name="filePath">The path where the file should be written.</param>
        /// <param name="fileInfo">The CVCIEFile structure containing the Data to write.</param>
        /// <returns>True if the file was written successfully; otherwise, false.</returns>
        public static bool WriteCIEFile(string filePath, CVCIEFile fileInfo)
            => WriteCIEFile(filePath, fileInfo, CVFileSaveMode.Synchronous);

        public static bool WriteCIEFile(string filePath, CVCIEFile fileInfo, CVFileSaveMode saveMode)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            if (fileInfo == null) return false;

            byte[] data = fileInfo.Data;
            return WriteCIEFile(
                filePath,
                fileInfo,
                data == null ? 0 : data.LongLength,
                stream =>
                {
                    if (data != null && data.Length > 0)
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }, saveMode);
        }

        /// <summary>
        /// Writes a CVCIE file while streaming its pixel payload from a caller-owned buffer.
        /// The callback completes synchronously before this method returns.
        /// </summary>
        /// <param name="filePath">The path where the file should be written.</param>
        /// <param name="fileInfo">The CVCIE header metadata. Its <see cref="CVCIEFile.Data"/> value is ignored.</param>
        /// <param name="dataLength">The exact number of payload bytes the callback will write.</param>
        /// <param name="writeData">Writes exactly <paramref name="dataLength"/> bytes to the supplied stream.</param>
        /// <returns>True if the complete file was written successfully; otherwise, false.</returns>
        public static bool WriteCIEFile(string filePath, CVCIEFile fileInfo, long dataLength, Action<Stream> writeData)
            => WriteCIEFile(filePath, fileInfo, dataLength, writeData, CVFileSaveMode.Synchronous);

        public static bool WriteCIEFile(
            string filePath,
            CVCIEFile fileInfo,
            long dataLength,
            Action<Stream> writeData,
            CVFileSaveMode saveMode)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            if (fileInfo == null || writeData == null || dataLength < 0) return false;
            uint version = fileInfo.Version;
            if (version is < 1 or > 3 || (version != 2 && dataLength > int.MaxValue)) return false;

            try
            {
                byte[] srcFileNameBytes = Encoding1.GetBytes(fileInfo.SrcFileName ?? string.Empty);
                long fileLength = checked(HeaderSize + sizeof(uint) + sizeof(int) + (long)srcFileNameBytes.Length
                    + (version == 3 ? sizeof(int) : 0) + sizeof(float) + sizeof(int)
                    + sizeof(float) * (long)fileInfo.Channels + 3 * sizeof(int)
                    + (version == 2 ? sizeof(long) : sizeof(int)) + dataLength);
                return CVFileReadCache.WriteFile(filePath, fileLength, fs =>
                {
                    using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8, true))
                    {
                        WriteImageHeader(bw, fileInfo, version, srcFileNameBytes, dataLength);
                        bw.Flush();
                        long dataStart = fs.Position;
                        writeData(fs);
                        bw.Flush();
                        if (fs.Position - dataStart != dataLength)
                        {
                            throw new InvalidDataException(
                                $"The CVCIE payload writer produced {fs.Position - dataStart} bytes; expected {dataLength} bytes.");
                        }
                    }
                }, saveMode);
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
            uint version = fileInfo.Version;
            if (version is < 1 or > 3) return false;

            try
            {
                byte[] srcFileNameBytes = Encoding1.GetBytes(fileInfo.SrcFileName ?? string.Empty);
                byte[] data = fileInfo.Data;
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    WriteImageHeader(bw, fileInfo, version, srcFileNameBytes, data?.LongLength ?? 0);
                    if (data != null) bw.Write(data);
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

        private static void WriteImageHeader(BinaryWriter writer, CVCIEFile fileInfo, uint version, byte[] sourceName, long payloadLength)
        {
            writer.Write(MagicHeader.ToCharArray());
            writer.Write(version);
            writer.Write(sourceName.Length);
            writer.Write(sourceName);
            if (version == 3) writer.Write(fileInfo.NDPort);
            writer.Write(fileInfo.Gain);
            writer.Write(fileInfo.Channels);

            float[] exposures = fileInfo.Exp;
            for (int channel = 0; channel < fileInfo.Channels; channel++)
            {
                writer.Write(exposures != null && channel < exposures.Length ? exposures[channel] : 0f);
            }

            writer.Write(fileInfo.Cols);
            writer.Write(fileInfo.Rows);
            writer.Write(fileInfo.Bpp);
            if (version == 2) writer.Write(payloadLength);
            else writer.Write(checked((int)payloadLength));
        }

        /// <summary>
        /// Writes a CVRAW file (convenience wrapper for WriteCIEFile).
        /// </summary>
        /// <param name="filePath">The path where the file should be written.</param>
        /// <param name="fileInfo">The CVCIEFile structure containing the Data to write.</param>
        /// <returns>True if the file was written successfully; otherwise, false.</returns>
        public static bool WriteCVRaw(string filePath, CVCIEFile fileInfo)
            => WriteCVRaw(filePath, fileInfo, CVFileSaveMode.Synchronous);

        public static bool WriteCVRaw(string filePath, CVCIEFile fileInfo, CVFileSaveMode saveMode)
        {
            return WriteCIEFile(filePath, fileInfo, saveMode);
        }

        /// <summary>
        /// Writes a CVRAW file while streaming its pixel payload from a caller-owned buffer.
        /// </summary>
        public static bool WriteCVRaw(string filePath, CVCIEFile fileInfo, long dataLength, Action<Stream> writeData)
            => WriteCVRaw(filePath, fileInfo, dataLength, writeData, CVFileSaveMode.Synchronous);

        public static bool WriteCVRaw(
            string filePath,
            CVCIEFile fileInfo,
            long dataLength,
            Action<Stream> writeData,
            CVFileSaveMode saveMode)
        {
            return WriteCIEFile(filePath, fileInfo, dataLength, writeData, saveMode);
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
