using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.Engine.Services;
using FlowEngineLib.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class PoiDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [Display(Order = 10)]
        public DisplayAlgorithmTemplateSelection FilterTemplate { get; set; }

        [Display(Order = 20)]
        public DisplayAlgorithmTemplateSelection ReviseTemplate { get; set; }

        [Display(Order = 30)]
        public DisplayAlgorithmTemplateSelection OutputTemplate { get; set; }

        [DisplayName("存储类型")]
        [Display(Order = 40)]
        public POIStorageModel StorageModel
        {
            get => _storageModel;
            set
            {
                _storageModel = value;
                OnPropertyChanged();
            }
        }
        private POIStorageModel _storageModel = POIStorageModel.Db;

        [DisplayName("POI文件")]
        [DisplayAlgorithmFile]
        [PropertyVisibility(nameof(StorageModel), POIStorageModel.File)]
        [Display(Order = 50)]
        public string POIPointFileName { get; set; } = string.Empty;

        [DisplayName("亚像素")]
        [Display(Order = 60)]
        public bool IsSubPixel { get; set; }

        [DisplayName("计算色温波长")]
        [Display(Order = 70)]
        public bool IsCCTWave { get; set; } = true;

        public PoiDisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "POI模板",
                new TemplatePoi(),
                "请先选择关注点模板"))
        {
            FilterTemplate = new DisplayAlgorithmTemplateSelection(
                "过滤模板",
                new TemplatePoiFilterParam(),
                "需要选择关注点过滤模板",
                () => TemplatePoiFilterParam.Params.CreateEmpty(),
                editorIndexOffset: -1);
            ReviseTemplate = new DisplayAlgorithmTemplateSelection(
                "修正模板",
                new TemplatePoiReviseParam(),
                "需要选择关注点修正模板",
                () => TemplatePoiReviseParam.Params.CreateEmpty(),
                editorIndexOffset: -1);
            OutputTemplate = new DisplayAlgorithmTemplateSelection(
                "输出模板",
                new TemplatePoiOutputParam(),
                "需要选择关注点输出模板",
                () => TemplatePoiOutputParam.Params.CreateEmpty(),
                editorIndexOffset: -1);
        }
    }

    [DisplayAlgorithm(1, "POI", "数据提取算法")]
    public class AlgorithmPoi : DisplayAlgorithmBase<PoiDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmPoi(DeviceAlgorithm deviceAlgorithm)
            : base(new PoiDisplayAlgorithmConfig())
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out PoiParam poiParam) ||
                !TryGetTemplate(Config.FilterTemplate, out PoiFilterParam filter) ||
                !TryGetTemplate(Config.ReviseTemplate, out PoiReviseParam revise) ||
                !TryGetTemplate(Config.OutputTemplate, out PoiOutputParam output) ||
                !TryGetImageInput(out string imageFileName, out _))
            {
                return null;
            }

            return SendCommand(
                string.Empty,
                string.Empty,
                imageFileName,
                poiParam,
                filter,
                revise,
                output);
        }

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, PoiParam poiParam, PoiFilterParam filter, PoiReviseParam revise, PoiOutputParam output)
        {

            FileExtType fileExtType = FileExtType.CIE;
            if (Path.GetExtension(fileName).Contains("cvraw"))
            {
                fileExtType = FileExtType.Raw;
            }
            else if (Path.GetExtension(fileName).Contains("cvcie"))
            {
                fileExtType = FileExtType.CIE;
            }
            else if (Path.GetExtension(fileName).Contains("tif"))
            {
                fileExtType = FileExtType.Tif;
            }
            else
            {
                fileExtType = FileExtType.Src;
            }

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType},{ "DeviceCode", deviceCode }, { "DeviceType", deviceType } };

            Params.Add("TemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            if (filter.Id != -1)
                Params.Add("FilterTemplate", new CVTemplateParam() { ID = filter.Id, Name = filter.Name });
            if (revise.Id != -1)
                Params.Add("ReviseTemplate", new CVTemplateParam() { ID = revise.Id, Name = revise.Name });
            if (output.Id != -1)
                Params.Add("OutputTemplate", new CVTemplateParam() { ID = output.Id, Name = output.Name });

            if (Config.StorageModel == POIStorageModel.File)
            {
                Params.Add("POIStorageType", Config.StorageModel);
                Params.Add("POIPointFileName", Config.POIPointFileName);
            }

            Params.Add("IsSubPixel", Config.IsSubPixel);
            Params.Add("IsCCTWave", Config.IsCCTWave);

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_POI_GetData,
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
