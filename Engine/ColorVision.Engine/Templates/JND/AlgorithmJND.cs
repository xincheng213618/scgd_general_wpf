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

namespace ColorVision.Engine.Templates.JND
{

    [DisplayAlgorithm(3, "JND", "数据提取算法")]
    public class AlgorithmJND : DisplayAlgorithmBase<DualTemplateDisplayAlgorithmConfig>
    {
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmJND(DeviceAlgorithm deviceAlgorithm) 
            : base(new DualTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "JND模板",
                    new TemplateJND(),
                    "请先选择JND模板"),
                new DisplayAlgorithmTemplateSelection(
                    "关注点模板",
                    new TemplatePoi(),
                    "请先选择关注点模板")))
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out JNDParam param) ||
                !TryGetTemplate(Config.SecondaryTemplate, out PoiParam poiParam) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, poiParam, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public MsgRecord SendCommand(JNDParam param, PoiParam poiParam, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            
            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_OLED_JND_CalVas_GetData,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
