#pragma warning disable CS8601,CA1822
using FileServerPlugin;
using MQTTMessageLib.FileServer;
using NetMQ;
using NetMQ.Sockets;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Net
{
    public delegate void NetFileHandler(object sender, NetFileEvent arg);
    public class NetFileUtil
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(NetFileUtil));

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
            if(string.IsNullOrEmpty(fileName)) return string.Empty;
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
        private void DownloadFile(string serverEndpoint, string fileName, FileExtType extType)
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
                logger.Error(ex);
                client?.Close();
                client?.Dispose();
            }
            finally
            {
                CVCIEFileInfo fileInfo = default;
                if (bytes != null)
                {
                    if (extType == FileExtType.Src) fileInfo = new CVCIEFileInfo() { data = bytes, };
                    else if (extType == FileExtType.Raw || extType == FileExtType.CIE) fileInfo = DecodeCVFile(bytes, fileName);
                }
                handler?.Invoke(this, new NetFileEvent(code, fileName, fileInfo));
            }
        }

        private void UploadFile(string serverEndpoint, string fileName)
        {
            DealerSocket client = null;
            var message = new List<byte[]>();
            CVCIEFileInfo fileData = ReadLocalBinaryFile(fileName);
            int code = -1;
            bool? sendResult = false;
            string signRecv;
            if (fileData.data != null)
            {
                message.Add(fileData.data);
                try
                {
                    logger.Debug("Begin TrySendMultipartBytes ......");
                    client = new DealerSocket(serverEndpoint);
                    client?.SendMultipartBytes(message);
                    sendResult = client?.TryReceiveFrameString(TimeSpan.FromSeconds(30), out signRecv);
                    code = 0;
                    logger.Debug("End TrySendMultipartBytes.");
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
            else if (extType == FileExtType.Tif) data = ReadTIFImage(fileName);
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

        private CVCIEFileInfo DecodeCVFile(byte[] fileData, string fileName)
        {
            UInt32 w = 0, h = 0, bpp = 0, channels = 0;
            string srcFileName;
            byte[] imgData = null;
            float[] exp;
            CVCIEFileInfo result = new CVCIEFileInfo();
            if (CVFileUtils.GetParamFromFile(fileData, out w, out h, out bpp, out channels, out exp, out imgData, out srcFileName))
            {
                int Depth = CVFileUtils.GetMatDepth((int)bpp);

                OpenCvSharp.Mat src = new OpenCvSharp.Mat((int)h, (int)w, OpenCvSharp.MatType.MakeType(Depth, (int)channels), imgData);
                OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                if(bpp == 32)
                {
                    OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                }else if(bpp == 16)
                {
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U, 255.0 / 65535, 0.5);
                }
                int len = (int)(w * h * channels);
                result.data = new byte[len];
                Marshal.Copy(dst.Data, result.data, 0, len);
                logger.DebugFormat("Raw src file({0}) convert rgb.", fileName);
                result.fileType = FileExtType.Raw;
                result.width = (int)w;
                result.height = (int)h;
                result.bpp = 8;
                result.channels = (int)channels;
                result.depth = dst.Depth();
            }
            else
            {
                logger.ErrorFormat("Raw file({0}) is not exist.", fileName);
            }

            return result;
        }

        private CVCIEFileInfo ReadCVImageRaw(string fileName)
        {
            byte[] fileData = CVFileUtils.ReadBinaryFile(fileName);

            return DecodeCVFile(fileData, fileName);
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
                    if (srcFileName.EndsWith("cvraw")){
                        return ReadCVImageRaw(srcFileName);
                    }
                    else
                    {
                        result.data = CVFileUtils.ReadBinaryFile(srcFileName);
                        result.fileType = FileExtType.Src;
                        return result;
                    }
                }else if (bpp == 16 || bpp == 8)
                {
                    result.data = imgData;
                    result.fileType = FileExtType.Raw;
                    result.width = (int)w;
                    result.height = (int)h;
                    result.bpp = (int)bpp;
                    result.channels = (int)channels;
                    result.depth = GetDepth(bpp);
                    return result;
                }
                else if(bpp == 32)
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

        private int GetDepth(uint bpp)
        {
            if (bpp == 8) return 0;
            else if(bpp == 16) return 2;

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

        private CVCIEFileInfo ReadTIFImage(string fileName)
        {
            CVCIEFileInfo result = default;
            Mat src = Cv2.ImRead(fileName, ImreadModes.Unchanged);
            int channels = src.Channels();
            int bpp = GetBpp(src.Depth());
            if (bpp == 32)
            {
                OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
                src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                string fullFileName = FileCachePath + Path.DirectorySeparatorChar + Path.GetFileName(fileName);
                OpenCvSharp.Cv2.ImWrite(fullFileName, dst);
                result = ReadLocalBinaryFile(fullFileName);
            }
            else
            {
                result = ReadLocalBinaryFile(fileName);
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
