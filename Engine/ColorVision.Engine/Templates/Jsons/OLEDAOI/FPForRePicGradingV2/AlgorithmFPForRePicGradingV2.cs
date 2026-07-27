using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2
{
    [DisplayAlgorithm(56, "缺陷检测V2", "OLED")]
    public class AlgorithmFPForRePicGradingV2 : JsonDisplayAlgorithmBase<DualTemplateDisplayAlgorithmConfig>
    {
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmFPForRePicGradingV2(DeviceAlgorithm deviceAlgorithm)
            : base(new DualTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "缺陷检测V2模板",
                    new TemplateFPForRePicGradingV2(),
                    "请先选择缺陷检测V2模板"),
                new DisplayAlgorithmTemplateSelection(
                    "关注点模板",
                    new TemplatePoi(),
                    "请先选择关注点模板")))
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

            if (Config.SecondaryTemplate.TryGetValue(out PoiParam poiParam))
            {
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            }

            Params.Add("Version", "2.0");
            MsgSend msg = new()
            {
                EventName = "FPForRePicGradingV2",
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
