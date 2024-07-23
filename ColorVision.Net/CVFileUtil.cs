#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using CVCommCore.CVImage;
using MQTTMessageLib.FileServer;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace ColorVision.Net
{
    public static class CVFileUtil
    {
        const string MagicHeader = "CVCIE";
        const int HeaderSize = 5;
        const int MinimumFileSize = HeaderSize + 4; // Minimum file size to contain the header and version
        const int ExpectedVersion = 1;

        public static bool IsCIEFile(string? filePath)
        {
            if (!File.Exists(filePath)) return false;

            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            if (fs.Length < HeaderSize) return false;

            using BinaryReader br = new(fs);
            string fileHeader = new(br.ReadChars(HeaderSize));
            return fileHeader == MagicHeader;
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

            cvcie.FileExtType = filePath.Contains(".cvraw") ? FileExtType.Raw : filePath.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;

            uint ver = br.ReadUInt32();
            if (ver != ExpectedVersion) return -1;

            int fileNameLen = br.ReadInt32();
            if (fileNameLen > 0 && (fs.Position + fileNameLen) <= fs.Length)
                cvcie.srcFileName = new string(br.ReadChars(fileNameLen));

            cvcie.gain = br.ReadInt32();
            cvcie.channels = br.ReadInt32();

            if ((fs.Position + cvcie.channels * 4) > fs.Length) return -1; // Not enough data for channel exposure times

            cvcie.exp = new float[cvcie.channels];
            for (int i = 0; i < cvcie.channels; i++)
            {
                cvcie.exp[i] = br.ReadSingle();
            }

            cvcie.rows = br.ReadInt32();
            cvcie.cols = br.ReadInt32();
            cvcie.bpp = br.ReadInt32();

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
            if (version != ExpectedVersion) return -1; // Replace with the expected version number.
            startIndex += sizeof(uint);

            // Read and validate the file name length.
            int fileNameLength = BitConverter.ToInt32(fileData, startIndex);
            startIndex += sizeof(int);
            if (fileNameLength < 0 || startIndex + fileNameLength > fileData.Length) return -1;

            // Read the file name.
            cvcie.srcFileName = Encoding.ASCII.GetString(fileData, startIndex, fileNameLength);
            startIndex += fileNameLength;

            // Read additional fields with validation.
            if (!TryReadInt32(fileData, ref startIndex, out cvcie.gain)) return -1;
            if (!TryReadInt32(fileData, ref startIndex, out cvcie.channels)) return -1;
            if (!TryReadInt32(fileData, ref startIndex, out cvcie.rows)) return -1;
            if (!TryReadInt32(fileData, ref startIndex, out cvcie.cols)) return -1;
            if (!TryReadInt32(fileData, ref startIndex, out cvcie.bpp)) return -1;

            // Validate and read channel exposure times.
            if (startIndex + cvcie.channels * sizeof(float) > fileData.Length) return -1;
            cvcie.exp = new float[cvcie.channels];
            for (int i = 0; i < cvcie.channels; i++)
            {
                cvcie.exp[i] = BitConverter.ToSingle(fileData, startIndex);
                startIndex += sizeof(float);
            }

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
            int dataLen = br.ReadInt32();
            if (dataLen > 0 && fs.Position + dataLen <= fs.Length)
            {
                fileInfo.data = br.ReadBytes(dataLen);
                return true;
            }

            return false;
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

            // Read the data length
            int dataLen = br.ReadInt32();
            if (dataLen <= 0 || ms.Position + dataLen > ms.Length)
            {
                return false; // Invalid data length or not enough data
            }

            // Read the data
            fileInfo.data = br.ReadBytes(dataLen);
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

        public static int ReadCVRawChannel(string fileName, int channel, out CVCIEFile fileInfo)
        {
            if (ReadCVRaw(fileName, out CVCIEFile fileIn))
            {
                return ReadCVChannel(channel, fileIn, out fileInfo);
            }
            fileInfo = new CVCIEFile();
            return -1;
        }

        public static CVCIEFile OpenLocalFileChannel(string fileName, FileExtType extType, CVImageChannelType channelType)
        {
            if (channelType == CVImageChannelType.SRC)
            {
                if (extType == FileExtType.Raw)
                {
                    if (CVFileUtil.ReadCVRaw(fileName, out CVCIEFile meta))
                        return meta;
                }
                if (extType == FileExtType.CIE)
                {
                    if (CVFileUtil.ReadCVCIESrc(fileName, out CVCIEFile meta))
                        return meta;
                }
            }
            int channel = -1;
            CVCIEFile data = new();
            switch (channelType)
            {
                case CVImageChannelType.SRC:
                case CVImageChannelType.CIE_XYZ_X:
                case CVImageChannelType.RGB_R:
                    channel = 0;
                    break;
                case CVImageChannelType.CIE_XYZ_Y:
                case CVImageChannelType.RGB_G:
                    channel = 1;
                    break;
                case CVImageChannelType.CIE_XYZ_Z:
                case CVImageChannelType.RGB_B:
                    channel = 2;
                    break;
                default:
                    break;
            }
            if (channel >= 0)
            {

                if (extType == FileExtType.Raw)
                {
                    CVFileUtil.ReadCVRawChannel(fileName, channel, out data);
                }
                else if (extType == FileExtType.CIE)
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
                if (cvcie.srcFileName.EndsWith(".cvraw", StringComparison.OrdinalIgnoreCase))
                {
                    fileOut.FileExtType = FileExtType.Raw;
                    return Read(cvcie.srcFileName, out fileOut);
                }
                else
                {
                    if (cvcie.srcFileName!=null)
                    {
                        fileOut.data = ReadFile(cvcie.srcFileName);
                        fileOut.FileExtType = FileExtType.Tif;
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
        public static List<float[]> ReadCVCIE(string FileName)
        {
            List<float[]> bytes = new();
            CVCIEFile fileInfo = new();
            int index = ReadCIEFileHeader(FileName, out fileInfo);
            if (index < 0) return bytes;
            ReadCIEFileData(FileName, ref fileInfo, index);

            if (fileInfo.channels == 3)
            {
                int singleChannelLength = fileInfo.cols * fileInfo.rows * (fileInfo.bpp / 8);
                int singleChannel = fileInfo.cols * fileInfo.rows;

                float[] channel1Data = new float[singleChannel];
                float[] channel2Data = new float[singleChannel];
                float[] channel3Data = new float[singleChannel];
                Buffer.BlockCopy(fileInfo.data, 0, channel1Data, 0, singleChannelLength);
                Buffer.BlockCopy(fileInfo.data, singleChannelLength, channel2Data, 0, singleChannelLength);
                Buffer.BlockCopy(fileInfo.data, 2 * singleChannelLength, channel3Data, 0, singleChannelLength);

                bytes.Add(channel1Data);
                bytes.Add(channel2Data);
                bytes.Add(channel3Data);

                return bytes;
            }
            return bytes;

        }


        public static int ReadCVCIEXYZ(string FileName, int channel, out CVCIEFile fileOut)
        {
            int index = ReadCIEFileHeader(FileName, out fileOut);
            if (index < 0) return -1;
            ReadCIEFileData(FileName, ref fileOut, index);

            if (fileOut.channels > 1)
            {
                fileOut.FileExtType = FileExtType.Raw;
                fileOut.channels = 1;
                int len = fileOut.cols * fileOut.rows * fileOut.bpp / 8;
                byte[] data = new byte[len];
                Buffer.BlockCopy(fileOut.data, channel * len, data, 0, len);
                fileOut.data = data;
                return 0;
            }
            return  -2;
        }



        public static int ReadCVChannel(int channel, CVCIEFile fileIn, out CVCIEFile fileOut)
        {
            fileOut = new CVCIEFile();
            if (fileIn.data == null || fileIn.data.Length == 0) return -1;
            if (fileIn.FileExtType != FileExtType.Raw) return -1;
            if (fileIn.channels > channel)
            {
                fileOut.exp = fileIn.exp;
                fileOut.rows = fileIn.rows;
                fileOut.cols = fileIn.cols;
                fileOut.bpp = fileIn.bpp;
                fileOut.channels = 1;
                if (fileIn.channels > 1)
                {
                    Mat src = Mat.FromPixelData(fileIn.cols, fileIn.rows, MatType.MakeType(fileOut.Depth, fileIn.channels), fileIn.data);
                    Mat[] srces = src.Split();
                    int len = fileOut.cols * fileOut.rows * fileOut.bpp / 8;
                    fileOut.data = new byte[len];
                    Marshal.Copy(srces[channel].Data, fileOut.data, 0, len);
                }
                else
                {
                    fileOut.data = fileIn.data;
                }
                return 0;
            }

            return -2;
        }
    }
}
