using ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.PoiOutput;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POIRevise;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class MQTTAlgorithm : MQTTDeviceService<ConfigAlgorithm>
    {
        public DeviceAlgorithm DeviceAlgorithm { get; set; }

        public MQTTAlgorithm(DeviceAlgorithm device, ConfigAlgorithm Config) : base(Config)
        {
            DeviceAlgorithm = device;
            MsgReturnReceived += MQTTAlgorithm_MsgReturnReceived;   
            DeviceStatus = DeviceStatusType.Unknown;
        }

        private void MQTTAlgorithm_MsgReturnReceived(MsgReturn msg)
        {
            if (msg.DeviceCode != Config.Code) return;
            switch (msg.Code)
            {
                default:
                    break;
            }
        }

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

        public MsgRecord POI(string deviceCode, string deviceType, string fileName, PoiParam poiParam,POIFilterParam filter, PoiReviseParam revise, PoiOutputParam output,string sn)
        {
            sn = string.IsNullOrWhiteSpace(sn) ? DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff") : sn;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };

            Params.Add("TemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            if (filter.Id !=-1)
                Params.Add("FilterTemplate", new CVTemplateParam() { ID = filter.Id, Name = filter.Name });
            if (revise.Id != -1)
                Params.Add("ReviseTemplate", new CVTemplateParam() { ID = revise.Id, Name = revise.Name });
            if (output.Id != -1)
                Params.Add("OutputTemplate", new CVTemplateParam() { ID = output.Id, Name = output.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_POI_GetData,
                SerialNumber = sn,
                Params = Params
            };
            return PublishAsyncClient(msg);
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

        public MsgRecord FOV(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_FOV_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord SetLicense(string md5, string FileData)
        {
            MsgSend msg = new()
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

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_MTF_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }


        public MsgRecord SFR(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new()
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

            MsgSend msg = new()
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



        public MsgRecord LedCheck2(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber, Templates.LedCheck2.LedCheck2Param ledCheck2Param, PoiParam poiParam, CVOLEDCOLOR cOLOR)
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
