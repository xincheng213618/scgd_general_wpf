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
            if (msg.DeviceCode != Config.Code) return;
            IsRun = false;
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
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
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord UnInit()
        {
            MsgSend msg = new MsgSend { EventName = "UnInit" };
            return PublishAsyncClient(msg);
        }
        public void GetRawFiles(string deviceCode, string deviceType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.Raw }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } }
            };
            PublishAsyncClient(msg);
        }
        public void GetCIEFiles(string deviceCode, string deviceType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.CIE } ,{ "DeviceCode", deviceCode }, { "DeviceType", deviceType } }
            };
            PublishAsyncClient(msg);
        }

        public MsgRecord POI(string deviceCode, string deviceType, string fileName, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_POI_GetData,
                SerialNumber = sn,
                Params = Params
            };
            return PublishAsyncClient(msg);
        }
        public MsgRecord BuildPoi(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_Build_POI,
                SerialNumber = sn,
                Params = Params
            };
            return PublishAsyncClient(msg);
        }
        public MsgRecord BuildPoi(POILayoutTypes POILayoutReq, Dictionary<string, object> @params, string deviceCode, string deviceType, string fileName, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });
            Params.Add("POILayoutReq", POILayoutReq.ToString());
            foreach (var param in @params)
            {
                Params.Add(param.Key, param.Value);
            }

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_Build_POI,
                SerialNumber = sn,
                Params = Params
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord FOV(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_FOV_GetData,
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


        public MsgRecord MTF(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber, int poiId, string poiTempName)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });
            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiId, Name = poiTempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_MTF_GetData,
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

        public MsgRecord SFR(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_SFR_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }


        public MsgRecord Ghost(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_Ghost_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord Distortion(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_Distortion_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }



        public MsgRecord FocusPoints(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_LightArea_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord LedCheck(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber, int poiId, string poiTempName)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });
            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiId, Name = poiTempName });

            MsgSend msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_LedCheck_GetData,
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

        internal void Open(string deviceCode, string deviceType, string fileName, FileExtType extType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType }, { "FileExtType", extType } }
            };
            PublishAsyncClient(msg);
        }

        public void UploadCIEFile(string fileName)
        {
            //MsgSend msg = new MsgSend
            //{
            //    EventName = MQTTFileServerEventEnum.Event_File_Upload,
            //    ServiceName = Config.Code,
            //    Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", FileExtType.CIE } }
            //};
            //PublishAsyncClient(msg);
        }

        public MsgRecord CacheClear()
        {
            return PublishAsyncClient(new MsgSend { EventName = "" });
        }


    }
}
