using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{

    [DisplayAlgorithm(50, "LEDStripDetectionV2", "Json")]
    public class AlgorithmLEDStripDetectionV2 : DisplayAlgorithmBase
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmLEDStripDetectionV2(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateLEDStripDetectionV2(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayLEDStripDetectionV2(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public bool IsInversion { get => _IsInversion; set { _IsInversion = value; OnPropertyChanged(); } }
        private bool _IsInversion;

        public MsgRecord SendCommand(ParamBase param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            Params.Add("IsInversion", IsInversion);
            Params.Add("Version", "2.0");
            MsgSend msg = new()
            {
                EventName = "LEDStripDetection",
                SerialNumber = sn,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
