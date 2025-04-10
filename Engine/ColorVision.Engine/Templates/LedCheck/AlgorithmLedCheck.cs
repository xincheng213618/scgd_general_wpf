using ColorVision.Common.MVVM;
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.LedCheck
{
    public class AlgorithmLedCheck : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "像素级灯珠检测";
        public int Order { get; set; } = 20;

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenTemplatePoiCommand { get; set; }

        public AlgorithmLedCheck(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenTemplatePoiCommand = new RelayCommand(a => OpenTemplatePoi());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateLedCheck(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
        public int TemplatePoiSelectedIndex { get => _TemplatePoiSelectedIndex; set { _TemplatePoiSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiSelectedIndex;
        public void OpenTemplatePoi()
        {
            new TemplateEditorWindow(new TemplatePoi(), TemplatePoiSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }




        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayLedCheck(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public MsgRecord SendCommand(LedCheckParam param, PoiParam poiParam ,string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
           
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

            if (poiParam.Id == -1)
            {
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = string.Empty });
            }
            else
            {
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            }

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_LED_Check_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
