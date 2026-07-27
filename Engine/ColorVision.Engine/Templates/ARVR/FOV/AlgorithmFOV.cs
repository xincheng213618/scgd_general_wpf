using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.FOV
{
    [DisplayAlgorithm(53, "FOV1.0", "ARVR")]
    public class AlgorithmFOV : DisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmFOV(DeviceAlgorithm deviceAlgorithm)
            : base(new SingleTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "FOV模板",
                    new TemplateFOV(),
                    "请先选择FOV模板")))
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out FOVParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(string.Empty, string.Empty, imageFileName, fileExtType, param.Id, Config.Template.SelectedName);
        }

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_FOV_GetData,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
