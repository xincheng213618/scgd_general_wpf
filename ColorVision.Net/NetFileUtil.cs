#pragma warning disable CS8601,CA1822
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using log4net;
using System.Linq;
using System.Windows.Navigation;

namespace ColorVision.Net
{
    public delegate void NetFileHandler(object sender, NetFileEvent arg);
    public class NetFileUtil
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(NetFileUtil));

        public event NetFileHandler handler;
        private string FileCachePath;

        public NetFileUtil()
        {
        }

        public NetFileUtil(string fileCachePath)
        {
            this.FileCachePath = fileCachePath;
        }

        public string GetCacheFileFullName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(FileCachePath)) return string.Empty;
            DirectoryInfo directoryInfo = new DirectoryInfo(FileCachePath);
            if (!directoryInfo.Exists) return string.Empty;
            directoryInfo.GetFiles();
            foreach (var item in directoryInfo.GetFiles())
            {
                if (item.Name.Contains(fileName))
                    return item.FullName;
            }
            return string.Empty;
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
                if (param.IsLocal) OpenLocalFileChannel(param.FileURL, param.FileExtType, param.ChannelType);
                else if (!string.IsNullOrWhiteSpace(param.ServerEndpoint)) DownloadFileChannel(param.ServerEndpoint, param.FileURL, param.FileExtType, param.ChannelType);
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
            CVCIEFile fileInfo = new CVCIEFile();
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
                    code = CVFileUtil.ReadCVChannel(channel, fileInfo, ref fileInfo);
                }
            }
            handler?.Invoke(this, new NetFileEvent(FileEvent.FileDownload, code, fileName, fileInfo));
        }


        private int DoDownloadFile(string serverEndpoint, string fileName, FileExtType extType, ref CVCIEFile fileInfo)
        {
            int code = -1;
            DealerSocket client = null;
            byte[] bytes = null;
            try
            {
                client = new DealerSocket(serverEndpoint);
                List<byte[]> data = new List<byte[]>();
                bool? ret = client?.TryReceiveMultipartBytes(TimeSpan.FromSeconds(5),ref data,1);
                if (data != null && data.Count == 1)
                {
                    client?.SendFrame(fileName);
                    bytes = data[0];
                    if (!string.IsNullOrEmpty(FileCachePath))
                    {
                        string fullFileName = FileCachePath + Path.DirectorySeparatorChar + fileName;
                        WriteLocalBinaryFile(fullFileName, bytes);
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
                    if (extType == FileExtType.Tif)
                    {
                        fileInfo.data = bytes;
                        fileInfo.FileExtType = extType;
                    }
                    else if (extType == FileExtType.Raw || extType == FileExtType.CIE)
                    {
                        code = DecodeCVFile(bytes, fileName, ref fileInfo);
                        fileInfo.FileExtType = FileExtType.Raw;
                    }
                }
            }
            if (fileInfo.data != null && fileInfo.data.Length > 0) code = 0;
            return code;
        }
        private void DownloadFile(string serverEndpoint, string fileName, FileExtType extType)
        {
            CVCIEFile fileInfo = new CVCIEFile();
            int code = DoDownloadFile(serverEndpoint, fileName, extType, ref fileInfo);
            handler?.Invoke(this, new NetFileEvent(FileEvent.FileDownload, code, fileName, fileInfo));
        }

        private void UploadFile(string serverEndpoint, string fileName)
        {
            DealerSocket client = null;
            var message = new List<byte[]>();
            CVCIEFile fileData = new CVCIEFile();
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
                    handler?.Invoke(this, new NetFileEvent(FileEvent.FileUpload, code, fileName));
                }
            }
            else
            {
                handler?.Invoke(this, new NetFileEvent(FileEvent.FileUpload, code, fileName));
            }
        }

        public static int ReadLocalBinaryFile(string path,ref CVCIEFile fileInfo)
        {
            int code = -1;
            if (File.Exists(path))
            {
                System.IO.FileStream fileStream = new System.IO.FileStream(path, FileMode.Open, FileAccess.Read);
                System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(fileStream);
                //获取文件长度
                long length = fileStream.Length;
                if (length > 0)
                {
                    fileInfo.data = new byte[length];
                    //读取文件中的内容并保存到字节数组中
                    binaryReader.Read(fileInfo.data, 0, fileInfo.data.Length);
                    fileInfo.FileExtType = FileExtType.Tif;
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
            using (System.IO.FileStream fileStream = new System.IO.FileStream(fileName, FileMode.Create))
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
            CVCIEFile data = new CVCIEFile();
            int channel = -1;
            int readRet = -1;
            switch (channelType)
            {
                case CVImageChannelType.SRC:
                    if (extType == FileExtType.Raw)
                    {
                        readRet = CVFileUtil.ReadCVRaw(fileName, ref data);
                    }
                    else if (extType == FileExtType.CIE)
                    {
                        readRet = CVFileUtil.ReadCVCIESrc(fileName, ref data);
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
                    CVFileUtil.ReadCVRawChannel(fileName, channel, ref data);
                    code = 0;
                }
                else if (extType == FileExtType.CIE)
                {
                    CVFileUtil.ReadCVCIEXYZ(fileName, channel, ref data);
                    code = 0;
                }
            }

            handler?.Invoke(this, new NetFileEvent(FileEvent.FileDownload, code, fileName, data));
        }

        public void OpenLocalFile(string fileName, FileExtType extType)
        {
            CVCIEFile fileInfo = new CVCIEFile();
            int code = ReadLocalFile(fileName, extType, ref fileInfo);
            handler?.Invoke(this, new NetFileEvent(FileEvent.FileDownload, code, fileName, fileInfo));
        }

        public CVCIEFile OpenLocalCVFile(string fileName, FileExtType extType)
        {
            if (Path.GetExtension(fileName).Contains("cvraw"))
            {
                extType = FileExtType.Raw;
            }
            else if (Path.GetExtension(fileName).Contains("cvcie"))
            {
                extType = FileExtType.CIE;
            }
            CVCIEFile fileInfo = new CVCIEFile();
            int code = ReadLocalFile(fileName, extType, ref fileInfo);
            return fileInfo;
        }

        private int ReadLocalFile(string fileName, FileExtType extType, ref CVCIEFile fileInfo)
        {
            int code;
            if (!File.Exists(fileName)) return -1;
            if (extType == FileExtType.CIE) code = ReadCVCIE(fileName, ref fileInfo);

            else if (extType == FileExtType.Raw) code = ReadCVImageRaw(fileName, ref fileInfo);
            else if (extType == FileExtType.Src) code = ReadCVImageRaw(fileName, ref fileInfo);
            else if (extType == FileExtType.Tif) code = ReadLocalTIFImage(fileName, ref fileInfo);
            else code = ReadLocalBinaryFile(fileName, ref fileInfo);
            return code;
        }
        private int DecodeCVFile(byte[] fileData, string fileName,ref CVCIEFile fileInfo)
        {
            int code = -1;
            if (CVFileUtil.ReadByte(fileData, ref  fileInfo))
            {
                code = 0;
            }
            else
            {
                log.ErrorFormat("Raw file({0}) is not exist.", fileName);
            }
            return code;
        }

        private int DecodeCVFileTo8U(byte[] fileData, string fileName, ref CVCIEFile fileInfo)
        {
            int code = -1;
            if (CVFileUtil.ReadByte(fileData, ref fileInfo))
            {
                OpenCvSharp.Mat src = new OpenCvSharp.Mat(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
                OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                if (fileInfo.bpp == 32)
                {
                    OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                }
                else if (fileInfo.bpp == 16)
                {
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U, 255.0 / 65535, 0.5);
                }
                else
                {
                    dst = src;
                }
                int len = (int)(fileInfo.rows * fileInfo.cols * fileInfo.channels);
                fileInfo.data = new byte[len];
                Marshal.Copy(dst.Data, fileInfo.data, 0, len);
                log.DebugFormat("Raw src file({0}) convert rgb.", fileName);
                fileInfo.FileExtType = FileExtType.Raw;
                fileInfo.bpp = 8;
                code = 0;
            }
            else
            {
                log.ErrorFormat("Raw file({0}) is not exist.", fileName);
            }

            return code;
        }

        private int ReadCVImageRaw(string fileName,ref CVCIEFile fileInfo)
        {
            byte[] fileData = CVFileUtil.ReadFile(fileName);

            return DecodeCVFileTo8U(fileData, fileName,ref fileInfo);
        }

        private int ReadCVCIE(string fileName,ref CVCIEFile fileInfo)
        {
            byte[] fileData = CVFileUtil.ReadFile(fileName);
            if (fileData == null) return -1;
            int startIndex = CVFileUtil.ReadCIEFileHeader(fileData, ref fileInfo);
            
            //如果有原图则读取原图
             string cvrawPath = fileInfo.srcFileName;

            if (!string.IsNullOrEmpty(cvrawPath))
            {
                cvrawPath = Path.GetFileName(cvrawPath);

                if (!File.Exists(cvrawPath))
                {
                    cvrawPath = Path.GetDirectoryName(fileName) + "\\" + cvrawPath;
                }
                if (File.Exists(cvrawPath))
                {
                    fileData = null;

                    if (cvrawPath.EndsWith("cvraw", StringComparison.OrdinalIgnoreCase))
                    {
                        return ReadCVImageRaw(cvrawPath, ref fileInfo);
                    }
                    else
                    {
                        fileInfo.data = CVFileUtil.ReadFile(cvrawPath);
                        fileInfo.FileExtType = FileExtType.Src;
                        return 0;
                    }
                }
            }

            if (startIndex > 0)
            {
                int dataLen = BitConverter.ToInt32(fileData, startIndex);
                startIndex += 4;
                if (dataLen > 0)
                {
                    byte[] bytes = new byte[dataLen];
                    Buffer.BlockCopy(fileData, startIndex, bytes, 0, dataLen);
                    fileInfo.data = bytes;
                }
                fileData = null;
            }

            if (fileInfo.bpp == 16 || fileInfo.bpp == 8)
            {
                fileInfo.FileExtType = FileExtType.Raw;
                return 0;
            }
            else if (fileInfo.bpp == 32)
            {
                int len = (int)(fileInfo.rows * fileInfo.cols * (fileInfo.bpp / 8));
                OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                if (fileInfo.channels == 3)
                {
                    byte[] data = new byte[len];
                    Buffer.BlockCopy(fileInfo.data, len, data, 0, data.Length);
                    OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)fileInfo.rows, (int)fileInfo.cols, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), data);
                    OpenCvSharp.Cv2.Normalize(src, src, 0, 1, OpenCvSharp.NormTypes.MinMax);
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U, 255);
                }
                else
                {
                    OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)fileInfo.cols, (int)fileInfo.rows, OpenCvSharp.MatType.MakeType(OpenCvSharp.MatType.CV_32F, 1), fileInfo.data);
                    OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                }

                byte[] data_dst = new byte[fileInfo.rows * fileInfo.cols];
                Marshal.Copy(dst.Data, data_dst, 0, data_dst.Length);
                fileInfo.data = data_dst;
                fileInfo.FileExtType = FileExtType.Raw;
                fileInfo.bpp = 8;
                fileInfo.channels = 1;
                return 0;
            }
            return -1;
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

        private int ReadLocalTIFImage(string fileName,ref CVCIEFile result)
        {
            OpenCvSharp.Mat src = OpenCvSharp.Cv2.ImRead(fileName, OpenCvSharp.ImreadModes.Unchanged);
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
    public enum FileEvent
    {
        FileDownload,
        FileUpload,
    }

    public class NetFileEvent
    {
        public int Code { get; set; }
        public FileEvent EventName { get; set; }
        public string FileName { get; set; }
        public CVCIEFile FileData { get; set; }
        public NetFileEvent(FileEvent eventName, int code, string fileName, CVCIEFile fileData)
        {
            this.EventName = eventName;
            this.Code = code;
            this.FileName = fileName;
            this.FileData = fileData;
        }

        public NetFileEvent(FileEvent eventName, int code, string fileName) : this(eventName, code, fileName, default)
        {
        }
    }
}
