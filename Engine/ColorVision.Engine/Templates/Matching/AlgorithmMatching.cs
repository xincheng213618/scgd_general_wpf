using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

namespace ColorVision.Engine.Templates.Matching
{
    public class MatchingDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [System.ComponentModel.DisplayName("模板文件")]
        [DisplayAlgorithmFile("Tif Files (*.tif)|*.tif|All Files (*.*)|*.*")]
        public string TemplateFile { get; set; } = string.Empty;

        public MatchingDisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "参数模板",
                new TemplateMatch(),
                "请先选择模板匹配参数"))
        {
        }
    }

    [DisplayAlgorithm(99, nameof(ColorVision.Engine.Properties.Resources.TemplateMatching), "定位算法")]
    public class AlgorithmMatching : DisplayAlgorithmBase<MatchingDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmMatching(DeviceAlgorithm deviceAlgorithm)
            : base(new MatchingDisplayAlgorithmConfig())
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out MatchParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public MsgRecord SendCommand(MatchParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType )
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateFile", Config.TemplateFile);
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_MatchTemplate,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
