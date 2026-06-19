using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;

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




        public MsgRecord CallFunction(TemplateJsonParam modparam, string fileName, FileExtType fileExtType, string deviceCode, string deviceType)
        {
var Params = new Dictionary<string, object>() { { "InputParam", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = modparam.Id, Name = modparam.Name });
            MsgSend msg = new()
            {
                EventName = modparam.ModThirdPartyAlgorithmsModel.Code ?? string.Empty,
                SerialNumber = string.Empty,
                Params = Params
            };

            return PublishAsyncClient(msg);
        }


        public MsgRecord CacheClear()
        {  
            return PublishAsyncClient(new MsgSend { EventName = "" });
        }

    }
}
