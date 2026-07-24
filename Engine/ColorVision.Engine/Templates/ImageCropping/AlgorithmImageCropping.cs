using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.ImageCropping
{
    public class ImageCroppingDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [System.ComponentModel.DisplayName("点1")]
        public PointFloat Point1 { get; set; } = new();

        [System.ComponentModel.DisplayName("点2")]
        public PointFloat Point2 { get; set; } = new();

        [System.ComponentModel.DisplayName("点3")]
        public PointFloat Point3 { get; set; } = new();

        [System.ComponentModel.DisplayName("点4")]
        public PointFloat Point4 { get; set; } = new();

        public ImageCroppingDisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "发光区裁剪模板",
                new TemplateImageCropping(),
                "请先选择发光区裁剪模板"))
        {
        }
    }

    [DisplayAlgorithm(50, "发光区裁剪", "数据提取算法")]
    public class AlgorithmImageCropping : DisplayAlgorithmBase<ImageCroppingDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmImageCropping(DeviceAlgorithm deviceAlgorithm)
            : base(new ImageCroppingDisplayAlgorithmConfig())
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out ImageCroppingParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public MsgRecord SendCommand(ImageCroppingParam param,string deviceCode, string deviceType, string fileName, FileExtType fileExtType )
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            PointFloat[] ROI = new PointFloat[] { Config.Point1, Config.Point2, Config.Point3, Config.Point4 };
            Params.Add("ROI", ROI);
            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_Image_Cropping,
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
