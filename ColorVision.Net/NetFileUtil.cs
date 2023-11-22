using FileServerPlugin;
using log4net;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
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

        public void OpenRemoteFile(string serverEndpoint, string fileName, bool isCVCIE)
        {
            string cacheFile = GetCacheFileFullName(fileName);
            if (string.IsNullOrEmpty(cacheFile))
            {
                TaskStartDownloadFile(false, serverEndpoint, fileName, isCVCIE);
            }
            else
            {
                OpenLocalFile(cacheFile, isCVCIE);
            }
        }
        public void TaskStartDownloadFile(bool isLocal, string serverEndpoint, string fileName, bool isCVCIE)
        {
            Task t = new(() =>
            {
                if (isLocal) OpenLocalFile(fileName, isCVCIE);
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
                handler?.Invoke(this, new NetFileEvent(code, fileName, bytes));
            }
        }

        private void UploadFile(string serverEndpoint, string fileName)
        {
            DealerSocket client = null;
            var message = new List<byte[]>();
            byte[]? bytes = ReadLocalBinaryFile(fileName);
            int code = -1;
            if (bytes != null)
            {
                message.Add(bytes);
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


        public static byte[]? ReadLocalBinaryFile(string path)
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.FileStream fileStream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(fileStream);
                //获取文件长度
                long length = fileStream.Length;
                byte[] bytes = new byte[length];
                //读取文件中的内容并保存到字节数组中
                binaryReader.Read(bytes, 0, bytes.Length);
                return bytes;
            }
            else
            {
                logger.ErrorFormat("File {0} is not exist.", path);
            }
            return null;
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

        public void OpenLocalFile(string fileName, bool isCVCIE)
        {
            byte[]? data = null;
            if(isCVCIE) data = ReadLocalBinaryCIEFile(fileName);
            else data = ReadLocalBinaryFile(fileName);
            if (data == null) handler?.Invoke(this, new NetFileEvent(-1, fileName, data));
            else handler?.Invoke(this, new NetFileEvent(0, fileName, data));
        }

        public void OpenLocalCIEFile(string fileName)
        {
            var data = ReadLocalBinaryCIEFile(fileName);
            if (data == null) handler?.Invoke(this, new NetFileEvent(-1, fileName, data));
            else handler?.Invoke(this, new NetFileEvent(0, fileName, data));
        }

        private byte[]? ReadLocalBinaryCIEFile(string fileName)
        {
            return ReadCVImage(fileName);
        }

        private byte[]? ReadCVImage(string fileName)
        {
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            if (CVFileUtils.GetParamFromFile(fileName, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                if (System.IO.File.Exists(srcFileName))
                {
                    imgData = CVFileUtils.ReadBinaryFile(srcFileName);
                    return imgData;
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

                    imgData = ReadLocalBinaryFile(srcFileName);
                    logger.WarnFormat("CIE src file({0}) is not exist. opencv real build.", srcFileName);
                    return imgData;
                }
            }
            else
            {
                logger.ErrorFormat("CIE file({0}) is not exist.", fileName);
            }

            return null;
        }
    }

    public class NetFileEvent
    {
        public int Code { get; set; }
        public string FileName { get; set; }
        public byte[]? FileData { get; set; }
        public NetFileEvent(int code, string fileName, byte[]? bytes)
        {
            this.Code = code;
            this.FileName = fileName;
            this.FileData = bytes;
        }

        public NetFileEvent(int code, string fileName) : this(code, fileName, null)
        {
        }
    }
}
