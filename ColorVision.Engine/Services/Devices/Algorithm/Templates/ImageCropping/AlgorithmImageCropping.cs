using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ImageCropping
{
    public class AlgorithmImageCropping : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "发光区裁剪";

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
            new WindowTemplate(new TemplateImageCropping(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;


        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayImageCropping(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        public Point Point1 { get => _Point1; set { _Point1 = value; NotifyPropertyChanged(); } }
        private Point _Point1;
        public Point Point2 { get => _Point2; set { _Point2 = value; NotifyPropertyChanged(); } }
        private Point _Point2;
        public Point Point3 { get => _Point3; set { _Point3 = value; NotifyPropertyChanged(); } }
        private Point _Point3;
        public Point Point4 { get => _Point4; set { _Point4 = value; NotifyPropertyChanged(); } }
        private Point _Point4;
         


        public MsgRecord SendCommand(ImageCroppingParam param,string deviceCode, string deviceType, string fileName, FileExtType fileExtType ,string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

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
