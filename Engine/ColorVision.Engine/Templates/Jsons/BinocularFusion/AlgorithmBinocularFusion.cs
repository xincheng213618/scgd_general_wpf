using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;


namespace ColorVision.Engine.Templates.Jsons.BinocularFusion
{
    [DisplayAlgorithm(12, "StereoFusion", "ARVR")]
    public class AlgorithmBinocularFusion : JsonDisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>
    {
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmBinocularFusion(DeviceAlgorithm deviceAlgorithm)
            : base(new SingleTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "双目融合模板",
                    new TemplateBinocularFusion(),
                    "请先选择双目融合模板")))
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });


            MsgSend msg = new()
            {
                EventName = "ARVR.BinocularFusion",
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
