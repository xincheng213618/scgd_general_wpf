using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.CADMapping
{


    public class AlgorithmCADMapping : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "CAD布点";

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmCADMapping(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new WindowTemplate(new TemplateCADMapping(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayCADMapping(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        public PointFloat Point1 { get => _Point1; set { _Point1 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point1 = new PointFloat();
        public PointFloat Point2 { get => _Point2; set { _Point2 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point2 = new PointFloat();
        public PointFloat Point3 { get => _Point3; set { _Point3 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point3 = new PointFloat();
        public PointFloat Point4 { get => _Point4; set { _Point4 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point4 = new PointFloat();


        public MsgRecord SendCommand(CADMappingParam param,string CADfilePath, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("CAD_PosFileName", CADfilePath);
            PointFloat[] ROI = new PointFloat[] { Point1, Point2, Point3, Point4 };
            Params.Add("ROI", ROI);
            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_POI_CADMapping,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
