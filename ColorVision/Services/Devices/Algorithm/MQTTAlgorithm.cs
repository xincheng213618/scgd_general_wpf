using ColorVision.Services.Msg;
using cvColorVision;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Devices.Algorithm
{
    public class MQTTAlgorithm : MQTTDeviceService<ConfigAlgorithm>
    {
        public static Dictionary<string, ObservableCollection<string>> ServicesDevices { get; set; } = new Dictionary<string, ObservableCollection<string>>();

        public string DeviceID { get => Config.Id; }

        public event MessageRecvHandler OnMessageRecved;

        public DeviceAlgorithm DeviceAlgorithm { get; set; }

        public MQTTAlgorithm(DeviceAlgorithm device, ConfigAlgorithm Config) : base(Config)
        {
            DeviceAlgorithm = device;
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatusType.Unknown;
        }


        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            IsRun = false;
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    //case "Init":
                    //    DeviceStatus = DeviceStatusType.Init;
                    //    break;
                    //case "UnInit":
                    //    DeviceStatus = DeviceStatusType.UnInit;
                    //    break;
                    case "SetParam":
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "Open":
                        DeviceStatus = DeviceStatusType.Opened;
                        break;

                    case MQTTAlgorithmEventEnum.Event_POI_GetData:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        DeviceStatus = DeviceStatusType.Opened;
                        break;
                    case "SaveLicense":
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                    //    break;
                    case MQTTFileServerEventEnum.Event_File_Upload:
                    case MQTTFileServerEventEnum.Event_File_List_All:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        break;
                    case "MTF":
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"{msg.EventName}执行成功", "ColorVision"));
                        break;
                    case "FOV":
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"{msg.EventName}执行成功", "ColorVision"));
                        break;
                    default:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "GetData":
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        DeviceStatus = DeviceStatusType.Opened;
                        break;
                    //case "Close":
                    //    DeviceStatus = DeviceStatusType.UnInit;
                    //    break;
                    //case "Open":
                    //    DeviceStatus = DeviceStatusType.UnInit;
                    //    break;
                    //case "Init":
                    //    DeviceStatus = DeviceStatusType.UnInit;
                    //    break;
                    //case "UnInit":
                    //    DeviceStatus = DeviceStatusType.UnInit;
                    //    break;
                    case "Calibrations":
                        break;
                    default:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        DeviceStatus = DeviceStatusType.Opened;
                        break;
                }
            }
        }

        public bool IsRun { get; set; }
        public MsgRecord Init()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                //Params = new Dictionary<string, object>() { { "SnID", SnID } , {"CodeID",Config.Code } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord UnInit()
        {
            MsgSend msg = new MsgSend { EventName = "UnInit" };
            return PublishAsyncClient(msg);
        }

        public void GetCIEFiles()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.CIE } }
            };
            PublishAsyncClient(msg);
        }

        public MsgRecord POI(string fileName, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = "POI",
                SerialNumber = sn,
                Params = Params
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord FOV(string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = "FOV",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord SetLicense(string md5, string FileData)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SaveLicense",
                Params = new Dictionary<string, object>() { { "FileName", md5 }, { "FileData", FileData }, { "eType", 0 } }
            };
            return PublishAsyncClient(msg);
        }


        public MsgRecord MTF(string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber, int poiId, string poiTempName)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });
            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiId, Name = poiTempName });

            MsgSend msg = new MsgSend
            {
                EventName = "MTF",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }




        private static string ToJsonFileList(ImageChannelType imageChannelType, params string[] FileNames)
        {
            Dictionary<string, object> file_data = new Dictionary<string, object>();

            file_data.Add("eCalibType", cvColorVision.CalibrationType.DarkNoise);

            List<Dictionary<string, object>> keyValuePairs = new List<Dictionary<string, object>>();
            foreach (var item in FileNames)
            {
                Dictionary<string, object> keyValuePairs1 = new Dictionary<string, object>();
                keyValuePairs1.Add("Type", imageChannelType);
                keyValuePairs1.Add("filename", item);
                keyValuePairs.Add(keyValuePairs1);
            }
            file_data.Add("list", keyValuePairs);
            return JsonConvert.SerializeObject(file_data);
        }

        public MsgRecord SFR(string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = "SFR",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }


        public MsgRecord Ghost(string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = "Ghost",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord Distortion(string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = "Distortion",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }



        public MsgRecord FocusPoints(string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = "FocusPoints",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord LedCheck(string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = "LedCheck",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg, 60000);
        }


        public MsgRecord Close()
        {
            MsgSend msg = new MsgSend { EventName = "Close" };
            return PublishAsyncClient(msg);
        }

        internal void Open(string fileName, FileExtType extType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", extType } }
            };
            PublishAsyncClient(msg);
        }

        public void UploadCIEFile(string fileName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", FileExtType.CIE } }
            };
            PublishAsyncClient(msg);
        }

        public void GetRawFiles()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.Raw } }
            };
            PublishAsyncClient(msg);
        }
        public void CacheClear()
        {
            PublishAsyncClient(new MsgSend { EventName = "" });
        }
    }
}
