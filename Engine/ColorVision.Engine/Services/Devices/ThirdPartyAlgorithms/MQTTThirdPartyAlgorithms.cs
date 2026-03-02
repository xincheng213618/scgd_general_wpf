using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    public class MQTTThirdPartyAlgorithms : MQTTDeviceService<ConfigThirdPartyAlgorithms>
    {
        public DeviceThirdPartyAlgorithms DeviceThirdPartyAlgorithms { get; set; }

        public MQTTThirdPartyAlgorithms(DeviceThirdPartyAlgorithms device, ConfigThirdPartyAlgorithms Config) : base(Config)
        {
            DeviceThirdPartyAlgorithms = device;
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



        public MsgRecord Close()
        {
            MsgSend msg = new() { EventName = "Close" };
            return PublishAsyncClient(msg);
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



        public MsgRecord CallFunction(TemplateJsonParam modparam, string serialNumber, string fileName, FileExtType fileExtType, string deviceCode, string deviceType)
        {
            serialNumber = string.IsNullOrWhiteSpace(serialNumber) ? DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff") : serialNumber;
            
            var Params = new Dictionary<string, object>() { { "InputParam", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = modparam.Id, Name = modparam.Name });
            MsgSend msg = new()
            {
                EventName = modparam.ModThirdPartyAlgorithmsModel.Code ?? string.Empty,
                SerialNumber = serialNumber,
                Params = Params
            };

            return PublishAsyncClient(msg);
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
