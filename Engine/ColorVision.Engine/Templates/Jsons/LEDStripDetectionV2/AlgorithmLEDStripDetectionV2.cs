using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;


namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{
    public class LEDStripDetectionV2DisplayAlgorithmConfig : DualTemplateDisplayAlgorithmConfig
    {
        [System.ComponentModel.DisplayName("反向")]
        public bool IsInversion { get; set; }

        public LEDStripDetectionV2DisplayAlgorithmConfig()
            : base(
                new DisplayAlgorithmTemplateSelection(
                    "灯条检测模板",
                    new TemplateLEDStripDetectionV2(),
                    "请先选择灯条检测模板"),
                new DisplayAlgorithmTemplateSelection(
                    "关注点模板",
                    new TemplatePoi(),
                    "请先选择关注点模板"))
        {
        }
    }

    [DisplayAlgorithm(50, "灯条Poi中心计算", "Json")]
    public class AlgorithmLEDStripDetectionV2 : JsonDisplayAlgorithmBase<LEDStripDetectionV2DisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmLEDStripDetectionV2(DeviceAlgorithm deviceAlgorithm)
            : base(new LEDStripDetectionV2DisplayAlgorithmConfig())
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

            Params.Add("IsInversion", Config.IsInversion);
            Params.Add("Version", "2.0");
            MsgSend msg = new()
            {
                EventName = "LEDStripDetection",
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
