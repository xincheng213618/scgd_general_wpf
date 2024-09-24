using ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIRevise;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.PoiOutput;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIFilters;
using ColorVision.Net;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm
{

    public class MQTTAlgorithm : MQTTDeviceService<ConfigAlgorithm>
    {
        public DeviceAlgorithm Device { get; set; }
        private NetFileUtil netFileUtil = new NetFileUtil();


        public MQTTAlgorithm(DeviceAlgorithm device, ConfigAlgorithm Config) : base(Config)
        {
            Device = device;
            MsgReturnReceived += MQTTAlgorithm_MsgReturnReceived;   
            DeviceStatus = DeviceStatusType.Unknown;
            netFileUtil.handler += NetFileUtil_handler;
        }
        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Device.View.OpenImage(arg.FileData);
                });
            }
        }

        private void MQTTAlgorithm_MsgReturnReceived(MsgReturn msg)
        {
            if (msg.DeviceCode != Config.Code) return;
            switch (msg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(msg.Data));
                    switch (data.FileExtType)
                    {
                        case FileExtType.Raw:
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                data.Files.Reverse();
                                RawImageFiles = data.Files;
                            });
                            break;
                        case FileExtType.Src:
                            break;
                        case FileExtType.CIE:
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                data.Files.Reverse();
                                CIEImageFiles = data.Files;
                            });
                            break;
                        case FileExtType.Calibration:
                            break;
                        case FileExtType.Tif:
                            break;
                        default:
                            break;
                    }
                    break;
                case MQTTFileServerEventEnum.Event_File_Upload:
                    DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(msg.Data));
                    netFileUtil.TaskStartUploadFile(pm_up.IsLocal, pm_up.ServerEndpoint, pm_up.FileName);
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(msg.Data));
                    if (pm_dl != null)
                    {
                        if (!string.IsNullOrWhiteSpace(pm_dl.FileName)) netFileUtil.TaskStartDownloadFile(pm_dl.IsLocal, pm_dl.ServerEndpoint, pm_dl.FileName, FileExtType.CIE);
                    }
                    break;
                default:
                    List<AlgResultMasterModel> resultMaster = null;
                    if (msg.Data.MasterId > 0)
                    {
                        resultMaster = new List<AlgResultMasterModel>();
                        int MasterId = msg.Data.MasterId;
                        AlgResultMasterModel model = AlgResultMasterDao.Instance.GetById(MasterId);
                        if (model!=null)
                            resultMaster.Add(model);
                    }
                    else
                    {
                        resultMaster = AlgResultMasterDao.Instance.GetAllByBatchCode(msg.SerialNumber);
                    }
                    foreach (AlgResultMasterModel result in resultMaster)
                    {
                        Application.Current.Dispatcher.Invoke((Delegate)(() =>
                        {
                            Device.View.AlgResultMasterModelDataDraw(result);
                        }));
                    }
                    break;
            }
        }


        public List<string> RawImageFiles { get => _RawImageFiles; set { _RawImageFiles = value; NotifyPropertyChanged(); } } 
        private List<string> _RawImageFiles = new List<string>();
        public List<string> CIEImageFiles { get => _CIEImageFiles; set { _CIEImageFiles = value; NotifyPropertyChanged(); } }
        private List<string> _CIEImageFiles = new List<string>();


        public void GetRawFiles(string deviceCode, string deviceType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.Raw }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } }
            };
            PublishAsyncClient(msg);
        }

        public void GetCIEFiles(string deviceCode, string deviceType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.CIE } ,{ "DeviceCode", deviceCode }, { "DeviceType", deviceType } }
            };
            PublishAsyncClient(msg);
        }


        public MsgRecord BuildPoi(POIPointTypes POILayoutReq, Dictionary<string, object> @params, string deviceCode, string deviceType, string fileName, int pid, string tempName, string serialNumber)
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

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_Build_POI,
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

            MsgSend msg = new()
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

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_LightArea_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord LedCheck(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber, Templates.LedCheck.LedCheckParam ledCheckParam, PoiParam poiParam)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() {ID = ledCheckParam.Id, Name = ledCheckParam.Name });
            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_LED_Check_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg, 60000);
        }



        public MsgRecord LedCheck2(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber, ModThirdPartyParam ledCheck2Param, PoiParam poiParam, CVOLEDCOLOR cOLOR)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = ledCheck2Param.Id, Name = ledCheck2Param.Name });
            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            Params.Add("Color", cOLOR);

            MsgSend msg = new()
            {
                EventName = "FindDotsArray",
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg, 60000);
        }



        public MsgRecord LEDStripDetection(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_LED_StripDetection,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg, 60000);
        }

        internal void Open(string deviceCode, string deviceType, string fileName, FileExtType extType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType }, { "FileExtType", extType } }
            };
            PublishAsyncClient(msg);
        }

        public void UploadCIEFile(string fileName)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", FileExtType.CIE } }
            };
            PublishAsyncClient(msg);
        }

        public MsgRecord CacheClear()
        {
            return PublishAsyncClient(new MsgSend { EventName = "" });
        }


    }
}
