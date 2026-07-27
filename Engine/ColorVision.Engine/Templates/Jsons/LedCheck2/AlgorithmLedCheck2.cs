using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;


namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public enum CVOLEDCOLOR
    {
        BLUE = 0,
        GREEN = 1,
        RED = 2,
    };
    public class PointVM:ViewModelBase
    {
        public double X { get => _X; set { _X = value; OnPropertyChanged(); } }
        private double _X;
        public double Y { get => _Y; set { _Y = value; OnPropertyChanged(); } }
        private double _Y;

        public  PointFloat ToPointFloat()
        {
            return new PointFloat() { X = (float)X, Y = (float)Y };
        }
    }

    public class LedCheck2DisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [System.ComponentModel.DisplayName("颜色")]
        public CVOLEDCOLOR Color { get; set; }

        [System.ComponentModel.DisplayName("FDA类型")]
        public FlowEngineLib.Algorithm.CVOLED_FDAType FDAType { get; set; }

        [System.ComponentModel.DisplayName("点1")]
        public PointVM Point1 { get; set; } = new();

        [System.ComponentModel.DisplayName("点2")]
        public PointVM Point2 { get; set; } = new();

        [System.ComponentModel.DisplayName("点3")]
        public PointVM Point3 { get; set; } = new();

        [System.ComponentModel.DisplayName("点4")]
        public PointVM Point4 { get; set; } = new();

        public LedCheck2DisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "LedCheck模板",
                new TemplateLedCheck2(),
                "请先选择灯珠检测模板"))
        {
        }
    }

    [DisplayAlgorithm(21, "亚像素级灯珠检测", "定位算法")]
    public class AlgorithmLedCheck2 : JsonDisplayAlgorithmBase<LedCheck2DisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmLedCheck2(DeviceAlgorithm deviceAlgorithm)
            : base(new LedCheck2DisplayAlgorithmConfig())
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("Color", Config.Color);
            Params.Add("FDAType", Config.FDAType);


            PointFloat[] FixedLEDPoint = new PointFloat[] { Config.Point1.ToPointFloat(), Config.Point2.ToPointFloat(), Config.Point3.ToPointFloat(), Config.Point4.ToPointFloat() };
            Params.Add("FixedLEDPoint", FixedLEDPoint);

            MsgSend msg = new()
            {
                EventName = MQTTMessageLib.Algorithm.MQTTAlgorithmEventEnum.Event_OLED_FindDotsArrayMem_GetData,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
