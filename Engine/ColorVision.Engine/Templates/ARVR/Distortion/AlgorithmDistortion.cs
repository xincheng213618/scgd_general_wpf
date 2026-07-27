using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.Distortion
{

    [DisplayAlgorithmAttribute(55, nameof(ColorVision.Engine.Properties.Resources.DistortionEvaluation),"ARVR")]
    public class AlgorithmDistortion : DisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmDistortion(DeviceAlgorithm deviceAlgorithm)
            : base(new SingleTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "Distortion模板",
                    new TemplateDistortionParam(),
                    "请先选择Distortion模板")))
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out DistortionParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public MsgRecord SendCommand(DistortionParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

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
