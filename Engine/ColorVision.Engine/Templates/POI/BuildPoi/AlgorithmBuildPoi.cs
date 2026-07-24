using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Services;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class PointInt
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class CirclePoiLayoutConfig
    {
        [DisplayName("中心X")]
        public int CenterX { get; set; } = 500;

        [DisplayName("中心Y")]
        public int CenterY { get; set; } = 500;

        [DisplayName("半径")]
        public int Radius { get; set; } = 500;
    }

    public class RectPoiLayoutConfig
    {
        [DisplayName("中心X")]
        public int CenterX { get; set; } = 500;

        [DisplayName("中心Y")]
        public int CenterY { get; set; } = 500;

        [DisplayName("宽度")]
        public int Width { get; set; } = 500;

        [DisplayName("高度")]
        public int Height { get; set; } = 500;
    }

    public class PolygonPoiLayoutConfig
    {
        [DisplayName("点1")]
        public PointInt Point1 { get; set; } = new();

        [DisplayName("点2")]
        public PointInt Point2 { get; set; } = new() { X = 500 };

        [DisplayName("点3")]
        public PointInt Point3 { get; set; } = new() { X = 500, Y = 500 };

        [DisplayName("点4")]
        public PointInt Point4 { get; set; } = new() { Y = 500 };
    }

    public class CommonPoiLayoutConfig : ViewModelBase
    {
        [DisplayName("布局类型")]
        public POILayoutTypes LayoutType
        {
            get => _layoutType;
            set
            {
                _layoutType = value;
                OnPropertyChanged();
            }
        }
        private POILayoutTypes _layoutType = POILayoutTypes.Circle;

        [DisplayName("圆形")]
        [PropertyVisibility(nameof(LayoutType), POILayoutTypes.Circle)]
        public CirclePoiLayoutConfig Circle { get; set; } = new();

        [DisplayName("矩形")]
        [PropertyVisibility(nameof(LayoutType), POILayoutTypes.Rect)]
        public RectPoiLayoutConfig Rect { get; set; } = new();

        [DisplayName("四边形")]
        [PropertyVisibility(nameof(LayoutType), POILayoutTypes.PolygonFour)]
        public PolygonPoiLayoutConfig Polygon { get; set; } = new();
    }

    public class CadMappingPoiConfig
    {
        [DisplayName("点1")]
        public PointVM Point1 { get; set; } = new();

        [DisplayName("点2")]
        public PointVM Point2 { get; set; } = new();

        [DisplayName("点3")]
        public PointVM Point3 { get; set; } = new();

        [DisplayName("点4")]
        public PointVM Point4 { get; set; } = new();

        [DisplayName("CAD文件")]
        [DisplayAlgorithmFile]
        public string CADPosFileName { get; set; } = string.Empty;
    }

    public class BuildPoiDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [DisplayName("布点类型")]
        [Display(Order = 10)]
        public POIBuildType BuildType
        {
            get => _buildType;
            set
            {
                _buildType = value;
                OnPropertyChanged();
            }
        }
        private POIBuildType _buildType = POIBuildType.Common;

        [DisplayName("存储类型")]
        [Display(Order = 20)]
        public POIStorageModel StorageModel { get; set; } = POIStorageModel.Db;

        [DisplayName("常规布局")]
        [Display(Order = 30)]
        [PropertyVisibility(nameof(BuildType), POIBuildType.Common)]
        public CommonPoiLayoutConfig CommonLayout { get; set; } = new();

        [DisplayName("CAD映射")]
        [Display(Order = 40)]
        [PropertyVisibility(nameof(BuildType), POIBuildType.CADMapping)]
        public CadMappingPoiConfig CadMapping { get; set; } = new();

        public BuildPoiDisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "BuildPoi模板",
                new TemplateBuildPoi(),
                "请先选择BuildPoi模板"))
        {
        }
    }

    [DisplayAlgorithm(2, "关注点布点", "定位算法")]
    public class AlgorithmBuildPoi : DisplayAlgorithmBase<BuildPoiDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmBuildPoi(DeviceAlgorithm deviceAlgorithm)
            : base(new BuildPoiDisplayAlgorithmConfig())
        {
            Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out ParamBuildPoi param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            Dictionary<string, object> parameters = BuildLayoutParameters();
            return SendCommand(
                param,
                Config.CommonLayout.LayoutType,
                parameters,
                string.Empty,
                string.Empty,
                imageFileName,
                fileExtType);
        }

        private Dictionary<string, object> BuildLayoutParameters()
        {
            Dictionary<string, object> parameters = new();
            CommonPoiLayoutConfig layout = Config.CommonLayout;

            switch (layout.LayoutType)
            {
                case POILayoutTypes.Circle:
                    parameters.Add("LayoutCenter", new PointInt
                    {
                        X = layout.Circle.CenterX,
                        Y = layout.Circle.CenterY
                    });
                    parameters.Add("LayoutWidth", layout.Circle.Radius * 2);
                    parameters.Add("LayoutHeight", layout.Circle.Radius * 2);
                    break;
                case POILayoutTypes.Rect:
                    parameters.Add("LayoutCenter", new PointInt
                    {
                        X = layout.Rect.CenterX,
                        Y = layout.Rect.CenterY
                    });
                    parameters.Add("LayoutWidth", layout.Rect.Width);
                    parameters.Add("LayoutHeight", layout.Rect.Height);
                    break;
                case POILayoutTypes.PolygonFour:
                    parameters.Add("LayoutPolygon", new List<PointInt>
                    {
                        layout.Polygon.Point1,
                        layout.Polygon.Point2,
                        layout.Polygon.Point3,
                        layout.Polygon.Point4
                    });
                    break;
            }

            return parameters;
        }

        public MsgRecord SendCommand(ParamBuildPoi buildPOIParam, POILayoutTypes POILayoutReq, Dictionary<string, object> @params, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = buildPOIParam.Id, Name = buildPOIParam.Name });
            Params.Add("POILayoutReq", POILayoutReq.ToString());
            Params.Add("POIStorageType", Config.StorageModel);
            Params.Add("BuildType", Config.BuildType);
            if (Config.BuildType == POIBuildType.CADMapping)
            {
                CadMappingPoiConfig cad = Config.CadMapping;
                List<PointInt> pointInts = new List<PointInt>();
                pointInts.Add(new PointInt() { X = (int)cad.Point1.X, Y = (int)cad.Point1.Y });
                pointInts.Add(new PointInt() { X = (int)cad.Point2.X, Y = (int)cad.Point2.Y });
                pointInts.Add(new PointInt() { X = (int)cad.Point3.X, Y = (int)cad.Point3.Y });
                pointInts.Add(new PointInt() { X = (int)cad.Point4.X, Y = (int)cad.Point4.Y });

                Params.Add("LayoutPolygon", pointInts);

                PointFloat[] ROI = new PointFloat[] { cad.Point1.ToPointFloat(), cad.Point2.ToPointFloat(), cad.Point3.ToPointFloat(), cad.Point4.ToPointFloat() };

                Params.Add("CADMappingParam", new Dictionary<string, Object>() { { "CAD_MasterId", -1 },{ "ROI" , ROI },{ "CAD_PosFileName" , cad.CADPosFileName } });
            }

            foreach (var param in @params)
            {
                Params.Add(param.Key, param.Value);
            }
            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_Build_POI,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
