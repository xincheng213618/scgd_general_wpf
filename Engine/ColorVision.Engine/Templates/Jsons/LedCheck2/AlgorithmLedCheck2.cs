using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class PointVM:ViewModelBase
    {
        public double X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private double _X;
        public double Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private double _Y;

        public  PointFloat ToPointFloat()
        {
            return new PointFloat() { X = (float)X, Y = (float)Y };
        }
    }

    [DisplayAlgorithm(21, "亚像素级灯珠检测", "定位算法")]
    public class AlgorithmLedCheck2 : DisplayAlgorithmBase
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmLedCheck2(DeviceAlgorithm deviceAlgorithm)
        {
			Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateLedCheck2(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public FlowEngineLib.Algorithm.CVOLED_FDAType CVOLEDFDAType { get => _CVOLED_FDAType; set { _CVOLED_FDAType = value; NotifyPropertyChanged(); } }
        private FlowEngineLib.Algorithm.CVOLED_FDAType _CVOLED_FDAType;

        public PointVM Point1 { get => _Point1; set { _Point1 = value; NotifyPropertyChanged(); } }
        private PointVM _Point1 = new PointVM();
        public PointVM Point2 { get => _Point2; set { _Point2 = value; NotifyPropertyChanged(); } }
        private PointVM _Point2 = new PointVM();
        public PointVM Point3 { get => _Point3; set { _Point3 = value; NotifyPropertyChanged(); } }
        private PointVM _Point3 = new PointVM();
        public PointVM Point4 { get => _Point4; set { _Point4 = value; NotifyPropertyChanged(); } }
        private PointVM _Point4 = new PointVM();




        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayLedCheck2(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public MsgRecord SendCommand(ParamBase param, CVOLEDCOLOR cOLOR, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("Color", cOLOR);
            Params.Add("FDAType", CVOLEDFDAType);


            PointFloat[] FixedLEDPoint = new PointFloat[] { Point1.ToPointFloat(), Point2.ToPointFloat(), Point3.ToPointFloat(), Point4.ToPointFloat() };
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
