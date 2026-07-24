using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.LEDStripDetection
{
    public class LEDStripDetectionDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [System.ComponentModel.DisplayName("反向")]
        public bool IsInversion { get; set; }

        public LEDStripDetectionDisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "LEDStripDetection模板",
                new TemplateLEDStripDetection(),
                "请先选择LEDStripDetection模板"))
        {
        }
    }

    [DisplayAlgorithm(10, "LightBandDetection", "定位算法")]
    public class AlgorithmLEDStripDetection : DisplayAlgorithmBase<LEDStripDetectionDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmLEDStripDetection(DeviceAlgorithm deviceAlgorithm)
            : base(new LEDStripDetectionDisplayAlgorithmConfig())
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out LEDStripDetectionParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public MsgRecord SendCommand(LEDStripDetectionParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("IsInversion", Config.IsInversion);

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_LED_StripDetection,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
