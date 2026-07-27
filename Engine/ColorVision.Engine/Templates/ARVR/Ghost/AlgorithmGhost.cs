using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.Ghost
{
    public enum CVOLEDCOLOR
    {
        BLUE = 0,
        GREEN = 1,
        RED = 2,
    };

    public class GhostDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [System.ComponentModel.DisplayName("颜色")]
        public CVOLEDCOLOR Color { get; set; }

        public GhostDisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "Ghost模板",
                new TemplateGhost(),
                "请先选择Ghost模板"))
        {
        }
    }

    [DisplayAlgorithm(54, "Ghost1.0", "ARVR")]
    public class AlgorithmGhost : DisplayAlgorithmBase<GhostDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmGhost(DeviceAlgorithm deviceAlgorithm)
            : base(new GhostDisplayAlgorithmConfig())
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out GhostParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(string.Empty, string.Empty, imageFileName, fileExtType, param);
        }

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, GhostParam ghostParam)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };

            Params.Add("TemplateParam", new CVTemplateParam() { ID = ghostParam.Id, Name = ghostParam.Name });
            Params.Add("Color", Config.Color);

            MsgSend msg = new()
            {
                EventName = "Ghost",
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }

    }
}
