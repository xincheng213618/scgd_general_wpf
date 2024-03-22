#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Threading;
using System.Linq;
using OpenCvSharp;


namespace ColorVision.Net
{


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

        public static int SaveCVCIE(string FileName,string SavePath)
        {
            if (!File.Exists(FileName)) return -1;
            FileInfo fileInfo = new FileInfo(FileName);
            byte[] fileData = ReadFile(FileName);
            CVCIEFile fileOut = new CVCIEFile();
            if (ReadByte(fileData, ref fileOut))
            {
                if (fileOut.FileExtType == FileExtType.CIE)
                {
                    if (fileOut.srcFileName != null)
                    {
                        byte[] raw = ReadFile(Path.Combine(Path.GetDirectoryName(FileName)??string.Empty, fileOut.srcFileName));
                        CVCIEFile cvraw = new CVCIEFile();
                        if (ReadByte(raw, ref cvraw))
                        {
                            OpenCvSharp.Mat src = new OpenCvSharp.Mat(cvraw.cols, cvraw.rows, OpenCvSharp.MatType.MakeType(cvraw.Depth, cvraw.channels), cvraw.data);
                            src.SaveImage(SavePath + "\\" + fileInfo.Name + "_Src.tif");
                            //OpenCvSharp.Mat[] srces = src.Split();
                            //if (srces.Length == 1)
                            //{
                            //    srces[0].SaveImage(SavePath + "\\" + fileInfo.Name + "G.tif");
                            //}
                            //else
                            //{
                            //    srces[0].SaveImage(SavePath + "\\" + fileInfo.Name + "R.tif");
                            //    srces[1].SaveImage(SavePath + "\\" + fileInfo.Name + "G.tif");
                            //    srces[2].SaveImage(SavePath + "\\" + fileInfo.Name + "B.tif");
                            //}
                        }
                    }
                    if (fileOut.channels == 1)
                    {
                        int len = (int)(fileOut.cols * fileOut.rows * fileOut.bpp / 8);
                        byte[] data = new byte[len];
                        Buffer.BlockCopy(fileOut.data, 0 * len, data, 0, len);
                        OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)fileOut.rows, (int)fileOut.cols, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), data);
                        src.SaveImage(SavePath + "\\" + fileInfo.Name + "_Y.tif");
                    }
                    else if (fileOut.channels == 3)
                    {
                        for (int ch = 0; ch < 3; ch++)
                        {
                            int len = (int)(fileOut.cols * fileOut.rows * fileOut.bpp / 8);
                            byte[] data = new byte[len];
                            OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)fileOut.rows, (int)fileOut.cols, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), data);
                            src.SaveImage(SavePath + "\\" + fileInfo.Name + $"_{ch}.tif");
                        }
                    }
                }
                else if (fileOut.FileExtType == FileExtType.Raw)
                {
                    var cvraw = fileOut;
                    OpenCvSharp.Mat src = new OpenCvSharp.Mat(cvraw.cols, cvraw.rows, OpenCvSharp.MatType.MakeType(cvraw.Depth, cvraw.channels), cvraw.data);
                    src.SaveImage(SavePath + "\\" + fileInfo.Name + "Src.tif");
                    //OpenCvSharp.Mat[] srces = src.Split();
                    //if (srces.Length == 1)
                    //{
                    //    srces[0].SaveImage(SavePath + "\\" + fileInfo.Name + "G.tif");
                    //}
                    //else
                    //{
                    //    srces[0].SaveImage(SavePath + "\\" + fileInfo.Name + "R.tif");
                    //    srces[1].SaveImage(SavePath + "\\" + fileInfo.Name + "G.tif");
                    //    srces[2].SaveImage(SavePath + "\\" + fileInfo.Name + "B.tif");
                    //}
                }
                else
                {

                }


            }
            return 0;


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
                    int ret = -1;
                    if (fileOut.channels > 1)
                    {
                        fileOut.channels = 1;
                        int len = (int)(fileOut.cols * fileOut.rows * fileOut.bpp / 8);
                        byte[] data = new byte[len];
                        Buffer.BlockCopy(fileOut.data, channel * len, data, 0, len);
                        fileOut.data = data;
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

        public static int ReadCIEFileHeader(byte[] fileData, ref CVCIEFile cVCIEFileInfo)
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
