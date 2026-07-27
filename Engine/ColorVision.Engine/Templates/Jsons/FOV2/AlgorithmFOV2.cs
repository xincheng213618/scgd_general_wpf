using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;


namespace ColorVision.Engine.Templates.Jsons.FOV2
{

    [DisplayAlgorithm(53, "FOV2.0", "ARVR")]
    public class AlgorithmFOV2 : JsonDisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmFOV2(DeviceAlgorithm deviceAlgorithm)
            : base(new SingleTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "FOV2.0模板",
                    new TemplateDFOV(),
                    "请先选择FOV2.0模板")))
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("Version", "2.0");
            MsgSend msg = new()
            {
                EventName = "FOV",
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
