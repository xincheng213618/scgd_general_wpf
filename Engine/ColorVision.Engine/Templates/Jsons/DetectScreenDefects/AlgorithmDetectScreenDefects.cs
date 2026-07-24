using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.Jsons.DetectScreenDefects
{
    public class DetectScreenDefectsDisplayAlgorithmConfig : DualTemplateDisplayAlgorithmConfig
    {
        [System.ComponentModel.DisplayName("输出文件")]
        public string OutputFileName { get; set; } = "result.json";

        [System.ComponentModel.DisplayName("缓存长度")]
        public int BufferLen { get; set; } = 1024;

        public DetectScreenDefectsDisplayAlgorithmConfig()
            : base(
                new DisplayAlgorithmTemplateSelection(
                    Properties.Resources.ScreenDefectDetection,
                    new TemplateDetectScreenDefects(),
                    "请先选择屏幕缺陷检测模板"),
                new DisplayAlgorithmTemplateSelection(
                    "ROI",
                    new TemplatePoi(),
                    "请选择有效的ROI模板",
                    selectedIndex: -1))
        {
        }
    }

    [DisplayAlgorithm(58, nameof(Properties.Resources.ScreenDefectDetection), "ARVR")]
    public class AlgorithmDetectScreenDefects : JsonDisplayAlgorithmBase<DetectScreenDefectsDisplayAlgorithmConfig>
    {
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService => Device.DService;
        public AlgorithmDetectScreenDefects(DeviceAlgorithm deviceAlgorithm)
            : base(new DetectScreenDefectsDisplayAlgorithmConfig())
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>()
            {
                { "ImgFileName", fileName },
                { "FileType", fileExtType },
                { "DeviceCode", deviceCode },
                { "DeviceType", deviceType },
                { "TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name } },
                { "OutputFileName", Config.OutputFileName },
                { "IsInversion", false },
                { "BufferLen", Config.BufferLen },
                { "Color", 1 },
                { "Channel", 1 }
            };

            if (Config.SecondaryTemplate.TryGetValue(out PoiParam poi))
            {
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = poi.Id, Name = poi.Name });
            }
            else
            {
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = -1, Name = null });
            }

            MsgSend msg = new()
            {
                EventName = "ARVR.DetectScreenDefects",
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
