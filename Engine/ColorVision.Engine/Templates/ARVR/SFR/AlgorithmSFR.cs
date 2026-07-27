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

namespace ColorVision.Engine.Templates.SFR
{

    [DisplayAlgorithm(51, "SFR1.0", "ARVR")]
    public class AlgorithmSFR : DisplayAlgorithmBase<DualTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmSFR(DeviceAlgorithm deviceAlgorithm)
            : base(new DualTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "SFR模板",
                    new TemplateSFR(),
                    "请先选择SFR模板"),
                new DisplayAlgorithmTemplateSelection(
                    "关注点模板",
                    new TemplatePoi(),
                    "请先选择关注点模板")))
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out SFRParam param) ||
                !TryGetTemplate(Config.SecondaryTemplate, out PoiParam poiParam) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(
                string.Empty,
                string.Empty,
                imageFileName,
                fileExtType,
                param.Id,
                Config.Template.SelectedName,
                poiParam.Id,
                Config.SecondaryTemplate.SelectedName);
        }

        public MsgRecord SendCommand(
            string deviceCode,
            string deviceType,
            string fileName,
            FileExtType fileExtType,
            int pid,
            string tempName,
            int poiId,
            string poiTempName)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiId, Name = poiTempName });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_SFR_GetData,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
