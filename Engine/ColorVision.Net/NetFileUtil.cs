#pragma warning disable CS8601,CA1822
using System.IO;
using System.Threading.Tasks;

namespace ColorVision.Net
{
    public delegate void NetFileHandler(object sender, NetFileEvent arg);
    public class NetFileUtil
    {
        public event NetFileHandler handler;
        public NetFileUtil()
        {
        }
        public void TaskStartDownloadFile(bool isLocal, string serverEndpoint, string fileName, CVType extType)
        {
            Task t = new Task(delegate
            {
                if (isLocal)
                {
                    OpenLocalFile(fileName, extType);
                }
            });
            t.Start();
        }
        public void TaskStartDownloadFile(bool isLocal, string fileName, CVType extType)
        {
            Task t = new Task(delegate
            {
                if (isLocal)
                {
                    OpenLocalFileChannel(fileName, extType);
                }
            });
            t.Start();
        }

        public void OpenLocalFileChannel(string fileName, CVType extType, CVImageChannelType channelType =CVImageChannelType.SRC)
        {
            int code = -1;
            int channel = -1;
            CVCIEFile data = default(CVCIEFile);
            switch (channelType)
            {
                case CVImageChannelType.SRC:
                    if (extType == CVType.Raw)
                    {
                        if (CVFileUtil.ReadCVRaw(fileName, out data))
                            code = 0;

                    }
                    else if (extType == CVType.CIE)
                    {
                        if (CVFileUtil.ReadCVCIE(fileName, out data))
                            code = 0;
                    }
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
                 if (extType == CVType.CIE)
                {
                    CVFileUtil.ReadCVCIEXYZ(fileName, channel, out data);
                    code = 0;
                }
            }

            handler?.Invoke(this, new NetFileEvent(FileEvent.FileDownload, code, fileName, data));
        }

        public void OpenLocalFile(string fileName, CVType extType)
        {
            int code = ReadLocalFile(fileName, extType, out CVCIEFile fileInfo);
            handler?.Invoke(this, new NetFileEvent(FileEvent.FileDownload, code, fileName, fileInfo));
        }
        public CVCIEFile OpenLocalCVFile(string fileName)
        {
            CVType extType = CVType.Src;
            if (Path.GetExtension(fileName).Contains("cvraw"))
            {
                extType = CVType.Raw;
            }
            else if (Path.GetExtension(fileName).Contains("cvcie"))
            {
                extType = CVType.CIE;
            }
            return OpenLocalCVFile(fileName, extType);
        }

        public CVCIEFile OpenLocalCVFile(string fileName, CVType extType)
        {
            ReadLocalFile(fileName, extType, out CVCIEFile fileInfo);
            return fileInfo;
        }

        private int ReadLocalFile(string fileName, CVType extType, out CVCIEFile fileInfo)
        {
            fileInfo = new CVCIEFile();
            if (extType == CVType.CIE)
            {
               CVFileUtil.ReadCVCIE(fileName, out fileInfo);
                return 0;
            }
            else if (extType == CVType.Raw)
            {
                CVFileUtil.ReadCVRaw(fileName, out fileInfo);
                return 0;
            }
            else if (extType == CVType.Src)
            {
                CVFileUtil.ReadCVRaw(fileName, out fileInfo);
                return 0;
            }
            return -1;
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
            EventName = eventName;
            Code = code;
            FileName = fileName;
            FileData = fileData;
        }

        public NetFileEvent(FileEvent eventName, int code, string fileName) : this(eventName, code, fileName, default)
        {
        }
    }
}
