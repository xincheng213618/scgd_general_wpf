#pragma warning disable CS8601,CA1822
using FileServerPlugin;
using log4net;
using MQTTMessageLib.FileServer;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ColorVision.Net
{
    public delegate void NetFileHandler(object sender, NetFileEvent arg);
    public class NetFileUtil
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(NetFileUtil));

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
            if (fileCache.ContainsKey(fileName)) return fileCache[fileName];
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
                else if (!string.IsNullOrWhiteSpace(serverEndpoint)) DownloadFile(serverEndpoint, fileName);
            });
            t.Start();
        }
        public void TaskStartUploadFile(bool isLocal, string serverEndpoint, string fileName)
        {
            Task t = new(() => { if (!isLocal && !string.IsNullOrWhiteSpace(serverEndpoint)) UploadFile(serverEndpoint, fileName); });
            t.Start();
        }
        private void DownloadFile(string serverEndpoint, string fileName)
        {
            DealerSocket client = null;
            byte[] bytes = null;
            int code = -1;
            try
            {
                client = new DealerSocket(serverEndpoint);
                List<byte[]> data = client?.ReceiveMultipartBytes(1);
                if (data != null && data.Count == 1)
                {
                    bytes = data[0];
                    code = 0;
                    if (!string.IsNullOrEmpty(FileCachePath))
                    {
                        string fullFileName = FileCachePath + "\\" + fileName;
                        WriteLocalBinaryFile(fullFileName, bytes);
                        fileCache.Add(fileName, fullFileName);
                    }
                }
                client?.Close();
                client?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                client?.Close();
                client?.Dispose();
            }
            finally
            {
                CVCIEFileInfo fileInfo = new CVCIEFileInfo() { data = bytes,};
                handler?.Invoke(this, new NetFileEvent(code, fileName, fileInfo));
            }
        }

        private void UploadFile(string serverEndpoint, string fileName)
        {
            DealerSocket client = null;
            var message = new List<byte[]>();
            CVCIEFileInfo fileData = ReadLocalBinaryFile(fileName);
            int code = -1;
            if (fileData.data != null)
            {
                message.Add(fileData.data);
                try
                {
                    client = new DealerSocket(serverEndpoint);
                    client?.TrySendMultipartBytes(TimeSpan.FromMilliseconds(5000), message);
                    code = 0;
                    client?.Close();
                    client?.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
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


        public static CVCIEFileInfo ReadLocalBinaryFile(string path)
        {
            CVCIEFileInfo result = new CVCIEFileInfo();
            if (System.IO.File.Exists(path))
            {
                System.IO.FileStream fileStream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(fileStream);
                //获取文件长度
                long length = fileStream.Length;
                if(length > 0)
                {
                    result.data = new byte[length];
                    //读取文件中的内容并保存到字节数组中
                    binaryReader.Read(result.data, 0, result.data.Length);
                    result.fileType = FileExtType.Src;
                }
                else
                {
                    logger.WarnFormat("File {0} Length is 0.", path);
                }
            }
            else
            {
                logger.ErrorFormat("File {0} is not exist.", path);
            }
            return result;
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
                    logger.InfoFormat(" Directory {0} is not exist. Create it.", dir);
                }
                catch (Exception ex) { logger.Error(ex); }
            }
        }

        public void OpenLocalFile(string fileName, FileExtType extType)
        {
            CVCIEFileInfo data = default;
            if(extType == FileExtType.CIE) data = ReadLocalBinaryCIEFile(fileName);
            else if(extType == FileExtType.Raw) data = ReadLocalBinaryRawFile(fileName);
            else data = ReadLocalBinaryFile(fileName);
            int code = 0;
            if (data.data == null) code = -1;
            handler?.Invoke(this, new NetFileEvent(code, fileName, data));
        }

        private CVCIEFileInfo ReadLocalBinaryRawFile(string fileName)
        {
            return ReadCVImageRaw(fileName);
        }

        public void OpenLocalCIEFile(string fileName)
        {
            var data = ReadLocalBinaryCIEFile(fileName);
            int code = 0;
            if (data.data == null) code = -1;
            handler?.Invoke(this, new NetFileEvent(code, fileName, data));
        }

        private CVCIEFileInfo ReadLocalBinaryCIEFile(string fileName)
        {
            return ReadCVImage(fileName);
        }

        private CVCIEFileInfo ReadCVImageRaw(string fileName)
        {
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            CVCIEFileInfo result = new CVCIEFileInfo();
            if (CVFileUtils.GetParamFromFile(fileName, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                int Depth = CVFileUtils.GetMatDepth((int)bpp);

                OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)h, (int)w, OpenCvSharp.MatType.MakeType(Depth, (int)channels), imgData);
                OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                int len = (int)(w * h);
                result.data = new byte[len];
                Marshal.Copy(dst.Data, result.data, 0, len);
                logger.DebugFormat("Raw src file({0}) convert rgb.", fileName);
                result.fileType = FileExtType.Raw;
                result.width = (int)w;
                result.height = (int)h;
                result.bpp = 8;
                result.channels = (int)channels;
            }
            else
            {
                logger.ErrorFormat("Raw file({0}) is not exist.", fileName);
            }

            return result;
        }

        private CVCIEFileInfo ReadCVImage(string fileName)
        {
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            CVCIEFileInfo result = new CVCIEFileInfo();
            if (CVFileUtils.GetParamFromFile(fileName, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                if (System.IO.File.Exists(srcFileName))
                {
                    result.data = CVFileUtils.ReadBinaryFile(srcFileName);
                    result.fileType = FileExtType.Src;
                    return result;
                }
                else
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

                    result = ReadLocalBinaryFile(srcFileName);
                    logger.WarnFormat("CIE src file({0}) is not exist. opencv real build.", srcFileName);
                }
            }
            else
            {
                logger.ErrorFormat("CIE file({0}) is not exist.", fileName);
            }

            return result;
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
