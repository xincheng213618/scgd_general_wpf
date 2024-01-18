#pragma warning disable CS8601,CA1822
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using NetMQ;
using NetMQ.Sockets;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Markup;
using log4net;

namespace ColorVision.Net
{
    public delegate void NetFileHandler(object sender, NetFileEvent arg);
    public class NetFileUtil
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(NetFileUtil));

        public event NetFileHandler handler;
        private Dictionary<string, string> fileCache;
        private string FileCachePath;

        public NetFileUtil(string fileCachePath)
        {
            this.fileCache = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(fileCachePath))
            {
                this.FileCachePath = fileCachePath;
                CreateDirectory(this.FileCachePath);
            }
        }
        public string GetCacheFileFullName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;
            else if (fileCache.ContainsKey(fileName)) return fileCache[fileName];
            else return string.Empty;
        }

        public void OpenRemoteFile(string serverEndpoint, string fileName, FileExtType extType)
        {
            string cacheFile = GetCacheFileFullName(fileName);
            if (string.IsNullOrEmpty(cacheFile))
            {
                TaskStartDownloadFile(false, serverEndpoint, fileName, extType);
            }
            else
            {
                OpenLocalFile(cacheFile, extType);
            }
        }
        public void TaskStartDownloadFile(bool isLocal, string serverEndpoint, string fileName, FileExtType extType)
        {
            Task t = new(() =>
            {
                if (isLocal) OpenLocalFile(fileName, extType);
                else if (!string.IsNullOrWhiteSpace(serverEndpoint)) DownloadFile(serverEndpoint, fileName, extType);
            });
            t.Start();
        }
        public void TaskStartDownloadFile(DeviceGetChannelResult param)
        {
            Task t = new(() =>
            {
                if (param.IsLocal) OpenLocalFileChannel(param.FileURL, param.FileType, param.ChannelType);
                else if (!string.IsNullOrWhiteSpace(param.ServerEndpoint)) DownloadFileChannel(param.ServerEndpoint, param.FileURL, param.FileType, param.ChannelType);
            });
            t.Start();
        }
        public void TaskStartUploadFile(bool isLocal, string serverEndpoint, string fileName)
        {
            if (isLocal)
            {
                //本地直接Copy
                //handler?.Invoke(this, new NetFileEvent(0, fileName));
            }
            else if (!string.IsNullOrWhiteSpace(serverEndpoint))
            {
                Task t = new(() => { UploadFile(serverEndpoint, fileName); });
                t.Start();
            }
        }

        private void DownloadFileChannel(string serverEndpoint, string fileName, FileExtType extType, CVImageChannelType channelType)
        {
            CVCIEFileInfo fileInfo = new CVCIEFileInfo();
            int code = DoDownloadFile(serverEndpoint, fileName, extType ,ref fileInfo);
            if (code == 0)
            {
                int channel = -1;
                switch (channelType)
                {
                    case CVImageChannelType.SRC:
                        break;
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
                    case CVImageChannelType.CIE_Lv:
                        break;
                    case CVImageChannelType.CIE_x:
                        break;
                    case CVImageChannelType.CIE_y:
                        break;
                    case CVImageChannelType.CIE_u:
                        break;
                    case CVImageChannelType.CIE_v:
                        break;
                    default:
                        break;
                }
                if (channel >= 0)
                {
                    code = CVFileUtils.ReadCVFile_channel(channel, fileInfo, ref fileInfo);
                }
            }
            handler?.Invoke(this, new NetFileEvent(code, fileName, fileInfo));
        }


        private int DoDownloadFile(string serverEndpoint, string fileName, FileExtType extType, ref CVCIEFileInfo fileInfo)
        {
            int code = -1;
            DealerSocket client = null;
            byte[] bytes = null;
            try
            {
                client = new DealerSocket(serverEndpoint);
                List<byte[]> data = client?.ReceiveMultipartBytes(1);
                if (data != null && data.Count == 1)
                {
                    bytes = data[0];
                    if (!string.IsNullOrEmpty(FileCachePath))
                    {
                        string fullFileName = FileCachePath + Path.DirectorySeparatorChar + fileName;
                        WriteLocalBinaryFile(fullFileName, bytes);
                        fileCache.Add(fileName, fullFileName);
                    }
                }
                client?.Close();
                client?.Dispose();
            }
            catch (Exception ex)
            {
                log.Error(ex);
                client?.Close();
                client?.Dispose();
            }
            finally
            {
                if (bytes != null)
                {
                    if (extType == FileExtType.Src)
                    {
                        fileInfo.data = bytes;
                        fileInfo.fileType = extType;
                    }
                    else if (extType == FileExtType.Raw || extType == FileExtType.CIE)
                    {
                        code = DecodeCVFile(bytes, fileName, ref fileInfo);
                        fileInfo.fileType = FileExtType.Raw;
                    }
                }
            }
            if (fileInfo.data != null && fileInfo.data.Length > 0) code = 0;
            return code;
        }
        private void DownloadFile(string serverEndpoint, string fileName, FileExtType extType)
        {
            CVCIEFileInfo fileInfo = new CVCIEFileInfo();
            int code = DoDownloadFile(serverEndpoint, fileName, extType, ref fileInfo);
            handler?.Invoke(this, new NetFileEvent(code, fileName, fileInfo));
        }

        private void UploadFile(string serverEndpoint, string fileName)
        {
            DealerSocket client = null;
            var message = new List<byte[]>();
            CVCIEFileInfo fileData = new CVCIEFileInfo();
            int code = ReadLocalBinaryFile(fileName, ref fileData);
            bool? sendResult = false;
            string signRecv;
            if (fileData.data != null)
            {
                message.Add(fileData.data);
                try
                {
                    log.Debug("Begin TrySendMultipartBytes ......");
                    client = new DealerSocket(serverEndpoint);
                    client?.SendMultipartBytes(message);
                    sendResult = client?.TryReceiveFrameString(TimeSpan.FromSeconds(30), out signRecv);
                    code = 0;
                    log.Debug("End TrySendMultipartBytes.");
                    client?.Close();
                    client?.Dispose();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    client?.Close();
                    client?.Dispose();
                }
                finally
                {
                    handler?.Invoke(this, new NetFileEvent(code, fileName));
                }
            }
            else
            {
                handler?.Invoke(this, new NetFileEvent(code, fileName));
            }
        }

        public static int ReadLocalBinaryFile(string path,ref CVCIEFileInfo fileInfo)
        {
            int code = -1;
            if (System.IO.File.Exists(path))
            {
                System.IO.FileStream fileStream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(fileStream);
                //获取文件长度
                long length = fileStream.Length;
                if (length > 0)
                {
                    fileInfo.data = new byte[length];
                    //读取文件中的内容并保存到字节数组中
                    binaryReader.Read(fileInfo.data, 0, fileInfo.data.Length);
                    fileInfo.fileType = FileExtType.Src;
                    code = 0;
                }
                else
                {
                    log.WarnFormat("File {0} Length is 0.", path);
                    code = -2;
                }
            }
            else
            {
                log.ErrorFormat("File {0} is not exist.", path);
            }
            return code;
        }

        public static void WriteLocalBinaryFile(string fileName, byte[] data)
        {
            using (System.IO.FileStream fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Create))
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(fileStream))
            {
                writer.Write(data);
            }
        }

        public static void CreateDirectory(string dir)
        {
            System.IO.DirectoryInfo folder = new System.IO.DirectoryInfo(dir);
            if (!folder.Exists)
            {
                try
                {
                    folder.Create();
                    log.InfoFormat(" Directory {0} is not exist. Create it.", dir);
                }
                catch (Exception ex) { log.Error(ex); }
            }
        }

        public void OpenLocalFileChannel(string fileName, FileExtType extType, CVImageChannelType channelType)
        {
            int code = -1;
            CVCIEFileInfo data = new CVCIEFileInfo();
            int channel = -1;
            int readRet = -1;
            switch (channelType)
            {
                case CVImageChannelType.SRC:
                    if (extType == FileExtType.Raw)
                    {
                        readRet = CVFileUtils.ReadCVFile_Raw(fileName, ref data);
                    }
                    else if (extType == FileExtType.CIE)
                    {
                        readRet = CVFileUtils.ReadCVFile_CIE_src(fileName, ref data);
                    }
                    if (readRet == 0) code = 0;
                    break;
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
                case CVImageChannelType.CIE_Lv:
                    break;
                case CVImageChannelType.CIE_x:
                    break;
                case CVImageChannelType.CIE_y:
                    break;
                case CVImageChannelType.CIE_u:
                    break;
                case CVImageChannelType.CIE_v:
                    break;
                default:
                    break;
            }
            if (channel >= 0)
            {
                if (extType == FileExtType.Raw)
                {
                    CVFileUtils.ReadCVFile_Raw_channel(fileName, channel, ref data);
                    code = 0;
                }
                else if (extType == FileExtType.CIE)
                {
                    CVFileUtils.ReadCVFile_CIE_XYZ(fileName, channel, ref data);
                    code = 0;
                }
            }

            handler?.Invoke(this, new NetFileEvent(code, fileName, data));
        }

        public void OpenLocalFile(string fileName, FileExtType extType)
        {
            int code = 0;
            CVCIEFileInfo fileInfo = new CVCIEFileInfo();
            if (extType == FileExtType.CIE) code = ReadLocalBinaryCIEFile(fileName, ref fileInfo);
            else if (extType == FileExtType.Raw) code = ReadLocalBinaryRawFile(fileName, ref fileInfo);
            else if (extType == FileExtType.Tif) code = ReadTIFImage(fileName,ref fileInfo);
            else code = ReadLocalBinaryFile(fileName, ref fileInfo);
            handler?.Invoke(this, new NetFileEvent(code, fileName, fileInfo));
        }

        private int ReadLocalBinaryRawFile(string fileName, ref CVCIEFileInfo fileInfo)
        {
            return ReadCVImageRaw(fileName, ref fileInfo);
        }

        public void OpenLocalCIEFile(string fileName)
        {
            CVCIEFileInfo data = new CVCIEFileInfo();
            int code = ReadLocalBinaryCIEFile(fileName, ref data);
            handler?.Invoke(this, new NetFileEvent(code, fileName, data));
        }

        private int ReadLocalBinaryCIEFile(string fileName, ref CVCIEFileInfo fileInfo)
        {
            return ReadCVImage(fileName,ref fileInfo);
        }

        private int DecodeCVFile(byte[] fileData, string fileName,ref CVCIEFileInfo fileInfo)
        {
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            int code = -1;
            if (CVFileUtils.GetParamFromFile(fileData, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                fileInfo.width = (int)w;
                fileInfo.height = (int)h;
                fileInfo.bpp = (int)bpp;
                fileInfo.channels = (int)channels;
                fileInfo.data = imgData;
                fileInfo.depth = GetDepth(bpp);
                code = 0;
            }
            else
            {
                log.ErrorFormat("Raw file({0}) is not exist.", fileName);
            }

            return code;
        }
        private int DecodeCVFileTo8U(byte[] fileData, string fileName, ref CVCIEFileInfo fileInfo)
        {
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            int code = -1;
            if (CVFileUtils.GetParamFromFile(fileData, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                int Depth = CVFileUtils.GetMatDepth((int)bpp);

                OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)h, (int)w, OpenCvSharp.MatType.MakeType(Depth, (int)channels), imgData);
                OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                if (bpp == 32)
                {
                    OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                }
                else if (bpp == 16)
                {
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U, 255.0 / 65535, 0.5);
                }
                else
                {
                    dst = src;
                }
                int len = (int)(w * h * channels);
                fileInfo.data = new byte[len];
                Marshal.Copy(dst.Data, fileInfo.data, 0, len);
                log.DebugFormat("Raw src file({0}) convert rgb.", fileName);
                fileInfo.fileType = FileExtType.Raw;
                fileInfo.width = (int)w;
                fileInfo.height = (int)h;
                fileInfo.bpp = 8;
                fileInfo.channels = (int)channels;
                fileInfo.depth = dst.Depth();
                code = 0;
            }
            else
            {
                log.ErrorFormat("Raw file({0}) is not exist.", fileName);
            }

            return code;
        }

        private int ReadCVImageRaw(string fileName,ref CVCIEFileInfo fileInfo)
        {
            byte[] fileData = CVFileUtils.ReadBinaryFile(fileName);

            return DecodeCVFileTo8U(fileData, fileName,ref fileInfo);
        }

        private int ReadCVImage(string fileName,ref CVCIEFileInfo fileInfo)
        {
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            if (CVFileUtils.GetParamFromFile(fileName, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                if (System.IO.File.Exists(srcFileName))
                {
                    if (srcFileName.EndsWith("cvraw", StringComparison.OrdinalIgnoreCase))
                    {
                        return ReadCVImageRaw(srcFileName,ref fileInfo);
                    }
                    else
                    {
                        fileInfo.data = CVFileUtils.ReadBinaryFile(srcFileName);
                        fileInfo.fileType = FileExtType.Src;
                        return 0;
                    }
                }
                else if (bpp == 16 || bpp == 8)
                {
                    fileInfo.data = imgData;
                    fileInfo.fileType = FileExtType.Raw;
                    fileInfo.width = (int)w;
                    fileInfo.height = (int)h;
                    fileInfo.bpp = (int)bpp;
                    fileInfo.channels = (int)channels;
                    fileInfo.depth = GetDepth(bpp);
                    return 0;
                }
                else if (bpp == 32)
                {
                    int len = (int)(w * h * (bpp / 8));
                    if (channels == 3)
                    {
                        byte[] data = new byte[len];
                        OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                        //Buffer.BlockCopy(imgData, 0, data, 0, data.Length);
                        //OpenCvSharp.Mat srcX = new OpenCvSharp.Mat((int)h, (int)w, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), data);
                        Buffer.BlockCopy(imgData, len, data, 0, data.Length);
                        OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)h, (int)w, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), data);
                        //Buffer.BlockCopy(imgData, len * 2, data, 0, data.Length);
                        //OpenCvSharp.Mat srcZ = new OpenCvSharp.Mat((int)h, (int)w, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), data);
                        //OpenCvSharp.Mat[] srcMerge = new OpenCvSharp.Mat[3] { srcX, srcY, srcZ };
                        //OpenCvSharp.Mat src = new OpenCvSharp.Mat();
                        //OpenCvSharp.Cv2.Merge(srcMerge, src);
                        OpenCvSharp.Cv2.Normalize(src, src, 0, 1, OpenCvSharp.NormTypes.MinMax);
                        src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U, 255);
                        OpenCvSharp.Cv2.ImWrite(srcFileName, dst);
                    }
                    else
                    {
                        OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)h, (int)w, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), imgData);
                        OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                        OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                        src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                        OpenCvSharp.Cv2.ImWrite(srcFileName, dst);
                    }

                    int code = ReadLocalBinaryFile(srcFileName,ref fileInfo);
                    log.WarnFormat("CIE src file({0}) is not exist. opencv real build.", srcFileName);
                    return code;
                }
            }
            else
            {
                log.ErrorFormat("CIE file({0}) is not exist.", fileName);
            }

            return -1;
        }

        private int GetDepth(uint bpp)
        {
            if (bpp == 8) return 0;
            else if (bpp == 16) return 2;

            return 0;
        }

        private int GetBpp(int depth)
        {
            int bpp = 8;
            switch (depth)
            {
                case 0:
                case 1:
                    bpp = 8;
                    break;
                case 2:
                case 3:
                    bpp = 16;
                    break;
                case 4:
                case 5:
                    bpp = 32;
                    break;
                case 6:
                    bpp = 64;
                    break;
                default:
                    break;
            }

            return bpp;
        }

        private int ReadTIFImage(string fileName,ref CVCIEFileInfo result)
        {
            Mat src = Cv2.ImRead(fileName, ImreadModes.Unchanged);
            int bpp = GetBpp(src.Depth());
            int code = -1;
            if (bpp == 32)
            {
                OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                string fullFileName = FileCachePath + Path.DirectorySeparatorChar + Path.GetFileName(fileName);
                OpenCvSharp.Cv2.ImWrite(fullFileName, dst);
                code = ReadLocalBinaryFile(fullFileName,ref result);
            }
            else
            {
                code = ReadLocalBinaryFile(fileName,ref result);
            }

            return code;
        }
    }

    public class NetFileEvent
    {
        public int Code { get; set; }
        public string FileName { get; set; }
        public CVCIEFileInfo FileData { get; set; }
        public NetFileEvent(int code, string fileName, CVCIEFileInfo fileData)
        {
            this.Code = code;
            this.FileName = fileName;
            this.FileData = fileData;
        }

        public NetFileEvent(int code, string fileName) : this(code, fileName, default)
        {
        }
    }
}
