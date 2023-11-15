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

        public void OpenRemoteFile(string serverEndpoint, string fileName)
        {
            string local = GetCacheFileFullName(fileName);
            if (string.IsNullOrEmpty(local))
            {
                TaskStartDownloadFile(serverEndpoint, fileName);
            }
            else
            {
                OpenLocalFile(local);
            }
        }
        public void TaskStartDownloadFile(string serverEndpoint, string fileName)
        {
            Task t = new(() => { DownloadFile(serverEndpoint, fileName); });
            t.Start();
        }
        public void TaskStartUploadFile(string serverEndpoint, string fileName)
        {
            Task t = new(() => { UploadFile(serverEndpoint, fileName); });
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

        public void OpenLocalFile(string fileName)
        {
            var data = ReadLocalBinaryFile(fileName);
            if (data == null) handler?.Invoke(this, new NetFileEvent(-1, fileName, data));
            else handler?.Invoke(this, new NetFileEvent(0, fileName, data));
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
