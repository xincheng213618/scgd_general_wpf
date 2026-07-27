using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;


namespace ColorVision.Engine.Templates.Jsons.PoiAnalysis
{

    [DisplayAlgorithm(53, "POI分析", "ARVR")]
    public class AlgorithmPoiAnalysis : JsonDisplayAlgorithmBase<DualTemplateDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }


        public AlgorithmPoiAnalysis(DeviceAlgorithm deviceAlgorithm)
            : base(new DualTemplateDisplayAlgorithmConfig(
                new DisplayAlgorithmTemplateSelection(
                    "POI分析模板",
                    new TemplatePoiAnalysis(),
                    "请先选择POI分析模板"),
                new DisplayAlgorithmTemplateSelection(
                    "关注点模板",
                    new TemplatePoi(),
                    "请先选择关注点模板")))
        {
            Device = deviceAlgorithm;
        }

        public static ObservableCollection<TemplateModel<TJPoiAnalysisParam>> Params => TemplatePoiAnalysis.Params;

        public static ObservableCollection<TemplateModel<PoiParam>> PoiParams => TemplatePoi.Params;

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

            if (Config.SecondaryTemplate.TryGetValue(out PoiParam poiParam))
            {
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            }

            Params.Add("Version", "1.0");
            MsgSend msg = new()
            {
                EventName = "PoiAnalysis",
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
