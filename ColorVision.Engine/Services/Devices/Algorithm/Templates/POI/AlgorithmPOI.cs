using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.PoiOutput;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIRevise;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI
{
    public class AlgorithmPOI : ViewModelBase, IAlgorithm
    {
        public string Name { get; set; } = "POI";

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTeplateCommand { get; set; }

        public RelayCommand OpenTeplatePOIFilterCommand { get; set; }

        public RelayCommand OpenTeplatePoiReviseCommand { get; set; }

        public RelayCommand OpenTeplatePoiOutputCommand { get; set; }


        public AlgorithmPOI(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTeplateCommand = new RelayCommand(a => OpenTeplate());
            OpenTeplatePOIFilterCommand = new RelayCommand(a => OpenTeplatePOIFilter());
            OpenTeplatePoiReviseCommand = new RelayCommand(a => OpenTeplatePoiRevise());
            OpenTeplatePoiOutputCommand = new RelayCommand(a => OpenTeplatePoiOutput());

        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTeplate()
        {
            new WindowTemplate(new TemplatePOI(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        public int TemplatePOIFilterSelectedIndex { get => _TemplatePOIFilterSelectedIndex; set { _TemplatePOIFilterSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePOIFilterSelectedIndex;

        public void OpenTeplatePOIFilter()
        {
            new WindowTemplate(new TemplatePOIFilterParam(), TemplatePOIFilterSelectedIndex -1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplatePoiReviseSelectedIndex { get => _TemplatePoiReviseSelectedIndex; set { _TemplatePoiReviseSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiReviseSelectedIndex;

        public void OpenTeplatePoiRevise()
        {
            new WindowTemplate(new TemplatePoiReviseParam(), TemplatePoiReviseSelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplatePoiOutputSelectedIndex { get => _TemplatePoiOutputSelectedIndex; set { _TemplatePoiOutputSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiOutputSelectedIndex;

        public void OpenTeplatePoiOutput()
        {
            new WindowTemplate(new TemplatePoiReviseParam(), TemplatePoiOutputSelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        


        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayPOI(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, PoiParam poiParam, POIFilterParam filter, PoiReviseParam revise, PoiOutputParam output, string sn)
        {
            sn = string.IsNullOrWhiteSpace(sn) ? DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff") : sn;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };

            Params.Add("TemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            if (filter.Id != -1)
                Params.Add("FilterTemplate", new CVTemplateParam() { ID = filter.Id, Name = filter.Name });
            if (revise.Id != -1)
                Params.Add("ReviseTemplate", new CVTemplateParam() { ID = revise.Id, Name = revise.Name });
            if (output.Id != -1)
                Params.Add("OutputTemplate", new CVTemplateParam() { ID = output.Id, Name = output.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_POI_GetData,
                SerialNumber = sn,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
