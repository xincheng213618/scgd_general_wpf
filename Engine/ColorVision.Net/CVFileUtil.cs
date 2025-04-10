#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using System;
using System.Globalization;
using System.IO;
using System.Text;


namespace ColorVision.Net
{

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

    public static class CVFileUtil
    {
        const string MagicHeader = "CVCIE";
        const int HeaderSize = 5;
        const int MinimumFileSize = HeaderSize + 4; // Minimum file size to contain the header and version

        public static bool IsCIEFile(string? filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
                if (fs.Length < HeaderSize) return false;

                using BinaryReader br = new(fs);
                string fileHeader = new(br.ReadChars(HeaderSize));
                return fileHeader == MagicHeader;

            }
            catch 
            {
                return false;
            }
        }

        public static bool IsCIEFile(byte[] fileData)
        {
            if (fileData == null || fileData.Length < HeaderSize) return false;
            // Convert the first 5 bytes to a string
            string fileHeader = Encoding.ASCII.GetString(fileData, 0, HeaderSize);
            return fileHeader == MagicHeader;
        }

        public static int ReadCIEFileHeader(string filePath, out CVCIEFile cvcie)
        {
            cvcie = new CVCIEFile();
            if (!File.Exists(filePath)) return -1;
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            if (fs.Length < MinimumFileSize) return -1;

            using BinaryReader br = new(fs);
            string fileHeader = new(br.ReadChars(HeaderSize));
            if (fileHeader != MagicHeader) return -1;

            cvcie.FileExtType = filePath.Contains(".cvraw") ? CVType.Raw : filePath.Contains(".cvsrc") ? CVType.Src : CVType.CIE;

            uint ver = br.ReadUInt32();
            cvcie.version = ver;
            if (ver !=1 && ver != 2)
            {
                return -1;
            }

            int fileNameLen = br.ReadInt32();
            if (fileNameLen > 0 && (fs.Position + fileNameLen) <= fs.Length)
                cvcie.srcFileName = new string(br.ReadChars(fileNameLen));

            cvcie.gain = br.ReadSingle();
            cvcie.channels = (int)br.ReadUInt32();

            if ((fs.Position + cvcie.channels * 4) > fs.Length) return -1; // Not enough data for channel exposure times

            cvcie.exp = new float[cvcie.channels];
            for (int i = 0; i < cvcie.channels; i++)
            {
                cvcie.exp[i] = br.ReadSingle();
            }

            cvcie.rows = (int)br.ReadUInt32();
            cvcie.cols = (int)br.ReadUInt32();
            cvcie.bpp = (int)br.ReadUInt32();

            cvcie.FilePath = filePath;
            return (int)fs.Position;
        }

        public static int ReadCIEFileHeader(byte[] fileData, out CVCIEFile  cvcie)
        {
            cvcie = new CVCIEFile();

            // Check if the data is null or does not meet the minimum required size.
            if (fileData == null || fileData.Length < MinimumFileSize) return -1;

            // Read and validate the file header.
            string fileHeader = Encoding.ASCII.GetString(fileData, 0, HeaderSize);
            if (fileHeader != MagicHeader) return -1;

            int startIndex = HeaderSize;
            uint version = BitConverter.ToUInt32(fileData, startIndex);

            cvcie.version = version;
            if (version != 1 && version != 2)
            {
                return -1;
            }

            startIndex += sizeof(uint);

            // Read and validate the file name length.
            int fileNameLength = BitConverter.ToInt32(fileData, startIndex);
            startIndex += sizeof(int);
            if (fileNameLength < 0 || startIndex + fileNameLength > fileData.Length) return -1;

            // Read the file name.
            cvcie.srcFileName = Encoding.GetEncoding("GBK").GetString(fileData, startIndex, fileNameLength);
            startIndex += fileNameLength;


            // Read additional fields with validation.
            if (!TryReadSingle(fileData, ref startIndex, out cvcie.gain)) return -1;
            if (!TryReadUInt32(fileData, ref startIndex, out cvcie.channels)) return -1;
            // Validate and read channel exposure times.
            if (startIndex + cvcie.channels * sizeof(float) > fileData.Length) return -1;
            cvcie.exp = new float[cvcie.channels];
            for (int i = 0; i < cvcie.channels; i++)
            {
                cvcie.exp[i] = BitConverter.ToSingle(fileData, startIndex);
                startIndex += sizeof(float);
            }
            if (!TryReadUInt32(fileData, ref startIndex, out cvcie.rows)) return -1;
            if (!TryReadUInt32(fileData, ref startIndex, out cvcie.cols)) return -1;
            if (!TryReadUInt32(fileData, ref startIndex, out cvcie.bpp)) return -1;

            return startIndex;
        }

        public static bool ReadCIEFileData(string filePath, ref CVCIEFile fileInfo, int dataStartIndex)
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            using BinaryReader br = new(fs);
            if (fs.Length < dataStartIndex)
            {
                return false; // The data start index is beyond the file length
            }

            fs.Position = dataStartIndex; // Set the stream position to the start of the data
                                          // Read the data length and data
                                          // Determine the data length based on the version
            long dataLen = fileInfo.version switch
            {
                2 => br.ReadInt64(),
                _ => br.ReadInt32()
            };

            // Ensure dataLen is within the file length
            if (dataLen > 0 && fs.Position + dataLen <= fs.Length)
            {
                // Initialize a buffer to hold the data
                fileInfo.data = new byte[dataLen];
                const int bufferSize = 81920; // 80 KB buffer size
                int bytesRead;
                long totalBytesRead = 0;

                // Read the data in chunks
                while (totalBytesRead < dataLen)
                {
                    int bytesToRead = (int)Math.Min(bufferSize, dataLen - totalBytesRead);
                    bytesRead = br.Read(fileInfo.data, (int)totalBytesRead, bytesToRead);
                    if (bytesRead == 0)
                    {
                        break; // End of file reached
                    }
                    totalBytesRead += bytesRead;
                }

                // Check if we have read the expected amount of data
                if (totalBytesRead == dataLen)
                {
                    return true;
                }
            }

            return false;
        }
        private static bool TryReadUInt32(byte[] data, ref int startIndex, out  int value)
        {
            if (startIndex + sizeof(int) > data.Length)
            {
                value = 0;
                return false;
            }
            value = (int)BitConverter.ToUInt32(data, startIndex);
            startIndex += sizeof(int);
            return true;
        }
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
        private static bool TryReadInt32(byte[] data, ref int startIndex, out int value)
        {
            if (startIndex + sizeof(int) > data.Length)
            {
                value = 0;
                return false;
            }
            value = BitConverter.ToInt32(data, startIndex);
            startIndex += sizeof(int);
            return true;
        }

        public static bool ReadCIEFileData(byte[] fileData, ref CVCIEFile fileInfo, int dataStartIndex)
        {
            if (fileData == null || fileData.Length < dataStartIndex)
            {
                return false; // The data start index is beyond the byte array length
            }

            using MemoryStream ms = new(fileData);
            using BinaryReader br = new(ms);

            ms.Position = dataStartIndex; // Set the stream position to the start of the data

            // Ensure there is enough data remaining for reading the data length
            if (ms.Length - ms.Position < sizeof(int))
            {
                return false; // Not enough data to read the data length
            }

            long dataLen = fileInfo.version switch
            {
                2 => br.ReadInt64(),
                _ => br.ReadInt32()
            };

            // Ensure dataLen is within the file length
            if (dataLen > 0 && ms.Position + dataLen <= ms.Length)
            {
                // Initialize a buffer to hold the data
                fileInfo.data = new byte[dataLen];
                const int bufferSize = 81920; // 80 KB buffer size
                int bytesRead;
                long totalBytesRead = 0;

                // Read the data in chunks
                while (totalBytesRead < dataLen)
                {
                    int bytesToRead = (int)Math.Min(bufferSize, dataLen - totalBytesRead);
                    bytesRead = br.Read(fileInfo.data, (int)totalBytesRead, bytesToRead);
                    if (bytesRead == 0)
                    {
                        break; // End of file reached
                    }
                    totalBytesRead += bytesRead;
                }

                // Check if we have read the expected amount of data
                if (totalBytesRead == dataLen)
                {
                    return true;
                }
            }

            return true;
        }

        public static bool Read(byte[] fileData, out CVCIEFile fileInfo)
        {
            int index = ReadCIEFileHeader(fileData, out fileInfo);
            if (index > 0)
                return ReadCIEFileData(fileData, ref fileInfo, index);
            return false;
        }
        public static bool Read(string filePath, out CVCIEFile fileInfo)
        {
            int index = ReadCIEFileHeader(filePath, out fileInfo);
            if (index > 0)
                return ReadCIEFileData(filePath, ref fileInfo, index);
            return false;
        }

        private static Encoding Encoding1 = Encoding.GetEncoding("GBK");
        public static int WriteFile(string fileName, CVCIEFile fileInfo)
        {

            using FileStream fileStream = new(fileName, FileMode.Create);
            using BinaryWriter writer = new(fileStream);
            int ver = 1;
            char[] hd = { 'C', 'V', 'C', 'I', 'E' };
            writer.Write(hd);
            writer.Write(ver);
            byte[] srcFileNameBytes = Encoding1.GetBytes(fileInfo.srcFileName);
            writer.Write(srcFileNameBytes.Length);
            writer.Write(srcFileNameBytes);
            writer.Write(fileInfo.gain);
            writer.Write(fileInfo.channels);
            for (int i = 0; i < fileInfo.exp.Length; i++)
            {
                writer.Write(fileInfo.exp[i]);
            }
            writer.Write(fileInfo.rows);
            writer.Write(fileInfo.cols);
            writer.Write(fileInfo.bpp);
            //
            writer.Write(fileInfo.data.Length);
            writer.Write(fileInfo.data);
            return 0;
        }

        public static bool ReadCVRaw(string fileName, out CVCIEFile fileInfo) => Read(fileName, out fileInfo);

        public static CVCIEFile OpenLocalFileChannel(string fileName, CVImageChannelType channelType)
        {
            string ext = Path.GetExtension(fileName)?.ToLower(CultureInfo.CurrentCulture);
            CVType fileExtType = ext.Contains(".cvraw") ? CVType.Raw : ext.Contains(".cvsrc") ? CVType.Src : CVType.CIE;
            return OpenLocalFileChannel(fileName, fileExtType, channelType);
        }

        public static bool ReadCVCIE(string fileName, out CVCIEFile fileInfo)
        {
            int startIndex = CVFileUtil.ReadCIEFileHeader(fileName, out fileInfo);
            if (startIndex < 0) return false;
            fileInfo.FilePath = fileName;

            if (!string.IsNullOrEmpty(fileInfo.srcFileName))
            {
                if (CVFileUtil.ReadCVCIESrc(fileName, out CVCIEFile fileInf))
                {
                    fileInfo = fileInf;
                    fileInfo.FilePath = fileName;
                    return true;
                }
            }

            return ReadCIEFileData(fileName, ref fileInfo, startIndex);
        }


        public static CVCIEFile OpenLocalFileChannel(string fileName, CVType extType, CVImageChannelType channelType)
        {
            if (channelType == CVImageChannelType.SRC)
            {
                if (extType == CVType.Raw)
                {
                    if (ReadCVRaw(fileName, out CVCIEFile meta))
                        return meta;
                }
                if (extType == CVType.CIE)
                {
                    if (ReadCVCIE(fileName, out CVCIEFile meta))
                        return meta;
                }
            }
            int channel = -1;
            CVCIEFile data = new();
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
                default:
                    break;
            }
            if (channel >= 0)
            {
                if (extType == CVType.CIE)
                {
                    CVFileUtil.ReadCVCIEXYZ(fileName, channel, out data);
                }
            }
            return data;
        }



        public static bool ReadCVCIESrc(string FileName, out CVCIEFile fileOut)
        {
            fileOut = new CVCIEFile();
            int index = ReadCIEFileHeader(FileName, out CVCIEFile cvcie);
            if (index < 0) return false;
            if (!File.Exists(cvcie.srcFileName))
                cvcie.srcFileName = Path.Combine(Path.GetDirectoryName(FileName) ?? string.Empty, cvcie.srcFileName);

            if (File.Exists(cvcie.srcFileName))
            {
                if (IsCIEFile(cvcie.srcFileName))
                {
                    fileOut.FileExtType = CVType.Raw;
                    return Read(cvcie.srcFileName, out fileOut);
                }
                else
                {
                    if (cvcie.srcFileName!=null)
                    {
                        fileOut.data = ReadFile(cvcie.srcFileName);
                        fileOut.FileExtType = CVType.Tif;
                        return true;
                    }
                }
            }
            return false;
        }

        public static byte[] ReadFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read);
                BinaryReader binaryReader = new(fileStream);
                //获取文件长度
                long length = fileStream.Length;
                byte[] bytes = new byte[length];
                //读取文件中的内容并保存到字节数组中
                binaryReader.Read(bytes, 0, bytes.Length);
                return bytes;
            }
            return null;
        }


        public static int ReadCVCIEXYZ(string FileName, int channel, out CVCIEFile fileOut)
        {
            int index = ReadCIEFileHeader(FileName, out fileOut);
            if (index < 0) return -1;
            ReadCIEFileData(FileName, ref fileOut, index);

            if (fileOut.channels > 1)
            {
                fileOut.FileExtType = CVType.Raw;
                fileOut.channels = 1;
                int len = fileOut.cols * fileOut.rows * fileOut.bpp / 8;
                byte[] data = new byte[len];
                Buffer.BlockCopy(fileOut.data, channel * len, data, 0, len);
                fileOut.data = data;
                return 0;
            }
            return  -2;
        }
    }
}
