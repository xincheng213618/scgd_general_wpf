#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using MQTTMessageLib.FileServer;
using OpenCvSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FileServerPlugin
{
    public struct CVCIEFileInfo
    {
        public FileExtType fileType;
        public int width;
        public int height;
        public int bpp;
        public int depth;
        public int channels;
        public int gain;
        public float[] exp;
        public string srcFileName;
        public byte[] data;
    }
    public static class CVFileUtils
    {
        private static System.Text.Encoding encoding = System.Text.Encoding.GetEncoding("GBK");

        public static string CreateDir(string path,string subPath)
        {
            if(!Directory.Exists(path)) Directory.CreateDirectory(path);
            string fullPath = Path.Combine(path, subPath);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public static int WriteBinaryFile_CVCIE(string fileName, int gain, float[] exp, int w, int h, int bpp, int channels, byte[] dst, string srcFileName)
        {
            using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fileStream))
            {
                int ver = 1;
                char[] hd = { 'C', 'V', 'C', 'I', 'E' };
                writer.Write(hd);
                writer.Write(ver);
                byte[] srcFileNameBytes = encoding.GetBytes(srcFileName);
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
            return 0;
        }

        public static int WriteBinaryFile_CVCIE(string fileName, CVCIEFileInfo fileInfo)
        {
            return WriteBinaryFile_CVCIE(fileName, fileInfo.gain, fileInfo.exp, fileInfo.width, fileInfo.height, fileInfo.bpp, fileInfo.channels, fileInfo.data, fileInfo.srcFileName);
        }

        public static int ReadCVFile_Raw(string fileName, ref CVCIEFileInfo fileInfo)
        {
            byte[] fileData = ReadBinaryFile(fileName);
            if (fileData == null) return -1;
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            if (GetParamFromFile(fileData, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                fileInfo.exp = exp;
                fileInfo.width = (int)w;
                fileInfo.height = (int)h;
                fileInfo.bpp = (int)bpp;
                fileInfo.channels = (int)channels;
                fileInfo.data = imgData;
                fileInfo.srcFileName = srcFileName;
                fileInfo.depth = GetMatDepth(bpp);

                return 0;
            }

            return -2;
        }

        public static int ReadCVFile_Raw_channel(string fileName, int channel, ref CVCIEFileInfo fileInfo)
        {
            CVCIEFileInfo fileIn = new CVCIEFileInfo();
            int ret = ReadCVFile_Raw(fileName, ref fileIn);
            if (ret == 0)
            {
                ret = ReadCVFile_channel(channel, fileIn, ref fileInfo);
            }

            return ret;
        }

        public static int ReadCVFile_CIE_src(string cieFileName, ref CVCIEFileInfo fileOut)
        {
            if (string.IsNullOrWhiteSpace(cieFileName)) return -1;
            if (System.IO.File.Exists(cieFileName))
            {
                byte[] fileData = ReadBinaryFile(cieFileName);
                if (fileData == null) return -1;
                UInt32 w = 0, h = 0, bpp = 0, channels = 0;
                string srcFileName;
                byte[] imgData = null;
                float[] exp;
                if (GetParamFromFile(fileData, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
                {
                    int ret = -1;
                    if (System.IO.File.Exists(srcFileName))
                    {
                        if (srcFileName.EndsWith(".cvraw", StringComparison.OrdinalIgnoreCase))
                        {
                            ret = ReadCVFile_Raw(srcFileName, ref fileOut);
                            fileOut.fileType = FileExtType.Raw;
                        }
                        else
                        {
                            fileOut.data = CVFileUtils.ReadBinaryFile(srcFileName);
                            fileOut.fileType = FileExtType.Tif;
                        }
                    }
                    return ret;
                }
            }
            return -2;
        }

        public static int ReadCVFile_CIE_XYZ(string cieFileName, int channel, ref CVCIEFileInfo fileOut)
        {
            if (string.IsNullOrWhiteSpace(cieFileName)) return -1;
            if (System.IO.File.Exists(cieFileName))
            {
                byte[] fileData = ReadBinaryFile(cieFileName);
                if (fileData == null) return -1;
                UInt32 w = 0, h = 0, bpp = 0, channels = 0;
                string srcFileName;
                byte[] imgData = null;
                float[] exp;
                if (GetParamFromFile(fileData, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
                {
                    fileOut.exp = exp;
                    fileOut.width = (int)w;
                    fileOut.height = (int)h;
                    fileOut.bpp = (int)bpp;
                    fileOut.depth = GetMatDepth(bpp);
                    fileOut.channels = 1;
                    int ret = -1;
                    if (channels > 1)
                    {
                        int len = (int)(fileOut.height * fileOut.width * fileOut.bpp / 8);
                        fileOut.data = new byte[len];
                        Buffer.BlockCopy(imgData, channel * len, fileOut.data, 0, len);
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

        public static int ReadCVFile_channel(int channel, CVCIEFileInfo fileIn, ref CVCIEFileInfo fileOut)
        {
            if (fileIn.data == null || fileIn.data.Length == 0) return -1;
            if (fileIn.fileType != FileExtType.Raw) return -1;
            if (fileIn.channels > channel)
            {
                fileOut.exp = fileIn.exp;
                fileOut.width = fileIn.width;
                fileOut.height = fileIn.height;
                fileOut.bpp = fileIn.bpp;
                fileOut.channels = 1;
                fileOut.depth = GetMatDepth(fileIn.bpp);
                if (fileIn.channels > 1)
                {
                    OpenCvSharp.Mat src = new OpenCvSharp.Mat(fileIn.height, fileIn.width, OpenCvSharp.MatType.MakeType(fileOut.depth, (int)fileIn.channels), fileIn.data);
                    OpenCvSharp.Mat[] srces = src.Split();
                    int len = (int)(fileOut.height * fileOut.width * fileOut.bpp / 8);
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

        public static bool GetFileHeader(string fileName, out uint w, out uint h, out uint bpp, out uint channels, out float[] exp, out string srcFileName)
        {
            byte[] fileData = ReadBinaryFile(fileName);

            uint gain = 0;
            if (fileData != null)
            {
                int startIndex = ReadCIEFileHeader(fileData, out w, out h, out bpp, out channels, out exp, out gain, out srcFileName);
                return startIndex > 0;
            }
            else
            {
                w = 0; h = 0; bpp = 0; channels = 0;
                exp = null;
                srcFileName = null;
            }

            return false;
        }

        private static int ReadCIEFileHeader(byte[] fileData, out uint w, out uint h, out uint bpp, out uint channels, out float[] exp, out uint gain, out string srcFileName)
        {
            int startIndex = 0;
            w = 0; h = 0; bpp = 0; channels = 0;
            exp = null;
            srcFileName = null;
            gain = 0;
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
                        srcFileName = encoding.GetString(fileNameBytes);
                    }
                    gain = BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    channels = BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    exp = new float[channels];
                    for (int i = 0; i < channels; i++)
                    {
                        exp[i] = BitConverter.ToSingle(fileData, startIndex);
                        startIndex += 4;
                    }
                    w = BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    h = BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    bpp = BitConverter.ToUInt32(fileData, startIndex);
                    startIndex += 4;
                    return startIndex;
                }
            }

            return -1;
        }

        public static bool GetParamFromFile(byte[] fileData, out uint w, out uint h, out uint bpp, out uint channels, out float[] exp, out byte[] imgdata, out string srcFileName)
        {
            imgdata = null;
            uint gain = 0;
            int startIndex = ReadCIEFileHeader(fileData, out w, out h, out bpp, out channels, out exp, out gain, out srcFileName);
            if (startIndex > 0)
            {
                //
                int dataLen = BitConverter.ToInt32(fileData, startIndex);
                startIndex += 4;
                if (dataLen > 0)
                {
                    byte[] bytes = new byte[dataLen];
                    Buffer.BlockCopy(fileData, startIndex, bytes, 0, dataLen);
                    imgdata = bytes;

                    return true;
                }
            }

            return false;
        }

        public static bool GetParamFromFile(string fileName, out uint w, out uint h, out uint bpp, out uint channels, out float[] exp, out byte[] imgdata, out string srcFileName)
        {
            byte[] fileData = ReadBinaryFile(fileName);

            return GetParamFromFile(fileData,out w,out h,out bpp,out channels,out exp,out imgdata, out srcFileName);
        }
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

        public static int GetMatDepth(uint bpp)
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

        public static int GetMatDepth(int bpp)
        {
            return GetMatDepth((uint)bpp);
        }
    }
}
