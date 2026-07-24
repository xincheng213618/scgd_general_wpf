using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;


namespace ColorVision.Engine.Templates.Jsons.Distortion2
{
    [DisplayAlgorithm(55, "畸变2.0", "ARVR")]

    public class AlgorithmDistortion2 : JsonDisplayAlgorithmBase<CieFileDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmDistortion2(DeviceAlgorithm deviceAlgorithm)
            : base(new CieFileDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "畸变模板",
                    new TemplateDistortion2(),
                    "请先选择畸变模板")))
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("CIEFileName", Config.CIEFileName);
            Params.Add("Version", "2.0");
            MsgSend msg = new()
            {
                EventName = "Distortion",
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
