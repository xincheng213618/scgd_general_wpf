using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.FindLightArea
{

    [DisplayAlgorithm(70, "发光区定位1", "基础算法")]
    public class AlgorithmRoi : DisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmRoi(DeviceAlgorithm deviceAlgorithm)
            : base(new SingleTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "发光区检测模板",
                    new TemplateRoi(),
                    "请先选择发光区检测模板")))
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out RoiParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public MsgRecord SendCommand(RoiParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_LightArea2_GetData,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
