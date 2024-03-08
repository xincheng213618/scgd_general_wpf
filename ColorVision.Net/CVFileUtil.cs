#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;


namespace ColorVision.Net
{
    public struct CVCIEFile
    {
        public FileExtType FileExtType;
        public int rows;
        public int cols;
        public int bpp;
        public readonly int Depth
        {
            get
            {
                return bpp switch
                {
                    8 => 0,
                    16 => 2,
                    32 => 5,
                    64 => 6,
                    _ => 0,
                };
            }
        }
        public int channels;
        public int gain;
        public float[] exp;
        public string srcFileName;
        public byte[] data;

        public CVCIEFile(CVCIEFile info)
        {
            FileExtType = info.FileExtType;
            rows = info.rows;
            cols = info.cols;
            bpp = info.bpp;
            channels = info.channels;
            gain = info.gain;
            exp = info.exp;
            srcFileName = info.srcFileName;
            data = info.data;
        }
    }


    public static class CVFileUtil
    {
        private static System.Text.Encoding Encoding = System.Text.Encoding.GetEncoding("GBK");
        public static int WriteFile(string fileName, CVCIEFile fileInfo)
        {

            using FileStream fileStream = new FileStream(fileName, FileMode.Create);
            using BinaryWriter writer = new BinaryWriter(fileStream);
            int ver = 1;
            char[] hd = { 'C', 'V', 'C', 'I', 'E' };
            writer.Write(hd);
            writer.Write(ver);
            byte[] srcFileNameBytes = Encoding.GetBytes(fileInfo.srcFileName);
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

        public static int ReadCVRaw(string fileName, ref CVCIEFile fileInfo)
        {
            byte[] fileData = ReadFile(fileName);
            if (fileData == null) return -1;
            if (ReadByte(fileData, ref fileInfo))
            {
                fileInfo.bpp = 8;
                return 0;
            }

            return -2;
        }

        public static int ReadCVRawChannel(string fileName, int channel, ref CVCIEFile fileInfo)
        {
            CVCIEFile fileIn = new CVCIEFile();
            int ret = ReadCVRaw(fileName, ref fileIn);
            if (ret == 0)
            {
                ret = ReadCVChannel(channel, fileIn, ref fileInfo);
            }

            return ret;
        }

        public static int ReadCVCIESrc(string cieFileName, ref CVCIEFile fileOut)
        {
            if (string.IsNullOrWhiteSpace(cieFileName)) return -1;
            if (File.Exists(cieFileName))
            {
                byte[] fileData = ReadFile(cieFileName);
                if (fileData == null) return -1;
                if (ReadByte(fileData, ref  fileOut))
                {
                    int ret = -1;
                    if (File.Exists(fileOut.srcFileName))
                    {
                        if (fileOut.srcFileName.EndsWith(".cvraw", StringComparison.OrdinalIgnoreCase))
                        {
                            ret = ReadCVRaw(fileOut.srcFileName, ref fileOut);
                            fileOut.FileExtType = FileExtType.Raw;
                        }
                        else
                        {
                            fileOut.data = ReadFile(fileOut.srcFileName);
                            fileOut.FileExtType = FileExtType.Tif;
                        }
                    }
                    return ret;
                }
            }
            return -2;
        }

        public static int ReadCVCIEXYZ(string cieFileName, int channel, ref CVCIEFile fileOut)
        {
            if (string.IsNullOrWhiteSpace(cieFileName)) return -1;
            if (File.Exists(cieFileName))
            {
                byte[] fileData = ReadFile(cieFileName);
                if (fileData == null) return -1;
                if (ReadByte(fileData, ref fileOut))
                {
                    fileOut.channels = 1;
                    int ret = -1;
                    fileOut.channels = 1;
                    if (fileOut.channels > 1)
                    {
                        int len = (int)(fileOut.cols * fileOut.rows * fileOut.bpp / 8);
                        fileOut.data = new byte[len];
                        Buffer.BlockCopy(fileOut.data, channel * len, fileOut.data, 0, len);
                    }
                    else
                    {
                        ret = -1;
                    }
                    return ret;
                }
            }
            return -2;
        }

        public static int ReadCVChannel(int channel, CVCIEFile fileIn, ref CVCIEFile fileOut)
        {
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
                    OpenCvSharp.Mat src = new OpenCvSharp.Mat(fileIn.cols, fileIn.rows, OpenCvSharp.MatType.MakeType(fileOut.Depth, (int)fileIn.channels), fileIn.data);
                    OpenCvSharp.Mat[] srces = src.Split();
                    int len = (int)(fileOut.cols * fileOut.rows * fileOut.bpp / 8);
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

        private static int ReadCIEFileHeader(byte[] fileData, ref CVCIEFile cVCIEFileInfo)
        {
            int startIndex = 0;
            if (fileData != null && fileData.Length > 5 && fileData[0] == 'C' && fileData[1] == 'V' && fileData[2] == 'C' && fileData[3] == 'I' && fileData[4] == 'E')
            {
                startIndex += 5;
                uint ver = BitConverter.ToUInt32(fileData, startIndex);
                startIndex += 4;
                if (ver == 1)
                {
                    int fileNameLen = BitConverter.ToInt32(fileData, startIndex);
                    startIndex += 4;
                    if (fileNameLen > 0)
                    {
                        byte[] fileNameBytes = new byte[fileNameLen];
                        Buffer.BlockCopy(fileData, startIndex, fileNameBytes, 0, fileNameLen);
                        startIndex += fileNameLen;
                        cVCIEFileInfo.srcFileName = Encoding.GetString(fileNameBytes);
                    }
                    cVCIEFileInfo.gain = (int)BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    cVCIEFileInfo.channels = (int)BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    cVCIEFileInfo.exp = new float[cVCIEFileInfo.channels];
                    for (int i = 0; i < cVCIEFileInfo.channels; i++)
                    {
                        cVCIEFileInfo.exp[i] = BitConverter.ToSingle(fileData, startIndex);
                        startIndex += 4;
                    }
                    cVCIEFileInfo.rows = (int)BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    cVCIEFileInfo.cols = (int)BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    cVCIEFileInfo.bpp = (int)BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    return startIndex;
                }
            }

            return -1;
        }

        public static bool ReadByte(byte[] fileData, ref CVCIEFile fileInfo)
        {
            int startIndex = ReadCIEFileHeader(fileData,ref fileInfo);
            if (startIndex > 0)
            {
                int dataLen = BitConverter.ToInt32(fileData, startIndex);
                startIndex += 4;
                if (dataLen > 0)
                {
                    byte[] bytes = new byte[dataLen];
                    Buffer.BlockCopy(fileData, startIndex, bytes, 0, dataLen);
                    fileInfo.data = bytes;
                    return true;
                }
            }

            return false;
        }

        public static bool ReadFile(string fileName, ref CVCIEFile fileInfo)
        {
            byte[] fileData = ReadFile(fileName);
            if (fileData == null) return false;
            return ReadByte(fileData, ref fileInfo);
        }

        public static byte[] ReadFile(string fileName)
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
    }
}
