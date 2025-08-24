using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.ImageCropping
{

    [DisplayAlgorithm(50, "发光区裁剪", "数据提取算法")]
    public class AlgorithmImageCropping : DisplayAlgorithmBase
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmImageCropping(DeviceAlgorithm deviceAlgorithm)
        {
			Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateImageCropping(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;


        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayImageCropping(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }
        public PointFloat Point1 { get => _Point1; set { _Point1 = value; OnPropertyChanged(); } }
        private PointFloat _Point1 = new PointFloat();
        public PointFloat Point2 { get => _Point2; set { _Point2 = value; OnPropertyChanged(); } }
        private PointFloat _Point2 = new PointFloat();
        public PointFloat Point3 { get => _Point3; set { _Point3 = value; OnPropertyChanged(); } }
        private PointFloat _Point3 = new PointFloat();
        public PointFloat Point4 { get => _Point4; set { _Point4 = value; OnPropertyChanged(); } }
        private PointFloat _Point4 = new PointFloat();


        public MsgRecord SendCommand(ImageCroppingParam param,string deviceCode, string deviceType, string fileName, FileExtType fileExtType ,string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            PointFloat[] ROI = new PointFloat[] { Point1, Point2, Point3, Point4 };
            Params.Add("ROI", ROI);
            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_Image_Cropping,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
