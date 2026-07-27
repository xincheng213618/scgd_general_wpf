using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;


namespace ColorVision.Engine.Templates.Jsons.BuildPOIAA
{
    [DisplayAlgorithm(53, "AA布点", "Json")]
    public class AlgorithmBuildPOIAA : JsonDisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmBuildPOIAA(DeviceAlgorithm deviceAlgorithm)
            : base(new SingleTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "AA布点模板",
                    new TemplateBuildPOIAA(),
                    "请先选择AA布点模板")))
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
                EventName = "ARVR.AA.FindPoints",
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
