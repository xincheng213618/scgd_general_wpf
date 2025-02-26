using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class AlgorithmLedCheck2 : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "亚像素级灯珠检测";
        public int Order { get; set; } = 21;

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenTemplatePoiCommand { get; set; }

        public AlgorithmLedCheck2(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenTemplatePoiCommand = new RelayCommand(a => OpenTemplatePoi());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateLedCheck2(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public int TemplatePoiSelectedIndex { get => _TemplatePoiSelectedIndex; set { _TemplatePoiSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiSelectedIndex;
        public void OpenTemplatePoi()
        {
            new TemplateEditorWindow(new TemplatePoi(), TemplatePoiSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public FlowEngineLib.Algorithm.CVOLED_FDAType CVOLEDFDAType { get => _CVOLED_FDAType; set { _CVOLED_FDAType = value; NotifyPropertyChanged(); } }
        private FlowEngineLib.Algorithm.CVOLED_FDAType _CVOLED_FDAType;

        public PointFloat Point1 { get => _Point1; set { _Point1 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point1;
        public PointFloat Point2 { get => _Point2; set { _Point2 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point2;
        public PointFloat Point3 { get => _Point3; set { _Point3 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point3;
        public PointFloat Point4 { get => _Point4; set { _Point4 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point4;


        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayLedCheck2(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public MsgRecord SendCommand(ParamBase param, PoiParam poiParam, CVOLEDCOLOR cOLOR, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            Params.Add("Color", cOLOR);
            Params.Add("FDAType", CVOLEDFDAType);


            PointFloat[] FixedLEDPoint = new PointFloat[] { Point1, Point2, Point3, Point4 };
            Params.Add("FixedLEDPoint", FixedLEDPoint);

            MsgSend msg = new()
            {
                EventName = MQTTMessageLib.Algorithm.MQTTAlgorithmEventEnum.Event_OLED_FindDotsArrayMem_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
