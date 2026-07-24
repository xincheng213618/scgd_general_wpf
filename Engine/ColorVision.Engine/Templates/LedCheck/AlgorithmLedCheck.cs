using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.LedCheck
{
    [DisplayAlgorithm(20, "PixelLedDetect", "定位算法")]
    public class AlgorithmLedCheck : DisplayAlgorithmBase<DualTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmLedCheck(DeviceAlgorithm deviceAlgorithm)
            : base(new DualTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "灯珠检测模板",
                    new TemplateLedCheck(),
                    "请先选择灯珠检测模板"),
                new DisplayAlgorithmTemplateSelection(
                    "关注点模板",
                    new TemplatePoi(),
                    "请先选择关注点模板",
                    () => TemplatePoi.Params.CreateEmpty())))
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out LedCheckParam param) ||
                !TryGetTemplate(Config.SecondaryTemplate, out PoiParam poiParam) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, poiParam, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public MsgRecord SendCommand(LedCheckParam param, PoiParam poiParam ,string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {

            // 组装参数，使用集合初始化器
            var Params = new Dictionary<string, object>
            {
                ["ImgFileName"] = fileName,
                ["FileType"] = fileExtType,
                ["DeviceCode"] = deviceCode,
                ["DeviceType"] = deviceType,
                ["TemplateParam"] = new CVTemplateParam { ID = param.Id, Name = param.Name },
                ["POITemplateParam"] = new CVTemplateParam
                {
                    ID = poiParam.Id,
                    Name = poiParam.Id == -1 ? string.Empty : poiParam.Name
                }
            };

            var msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_LED_Check_GetData,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
