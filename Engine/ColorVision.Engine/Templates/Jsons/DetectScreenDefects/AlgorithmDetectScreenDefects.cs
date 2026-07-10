using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.DetectScreenDefects
{
    [DisplayAlgorithm(58, nameof(Properties.Resources.ScreenDefectDetection), "ARVR")]
    public class AlgorithmDetectScreenDefects : DisplayAlgorithmBase
    {
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService => Device.DService;
        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenTemplatePoiCommand { get; set; }

        public AlgorithmDetectScreenDefects(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenTemplatePoiCommand = new RelayCommand(a => OpenTemplatePoi());
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public int TemplatePoiSelectedIndex { get => _TemplatePoiSelectedIndex; set { _TemplatePoiSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplatePoiSelectedIndex = -1;

        public string OutputFileName { get => _OutputFileName; set { _OutputFileName = value; OnPropertyChanged(); } }
        private string _OutputFileName = "result.json";

        public int BufferLen { get => _BufferLen; set { _BufferLen = value; OnPropertyChanged(); } }
        private int _BufferLen = 1024;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateDetectScreenDefects(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public void OpenTemplatePoi()
        {
            new TemplateEditorWindow(new TemplatePoi(), TemplatePoiSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayDetectScreenDefects(this);
            return UserControl;
        }

        public UserControl UserControl { get; set; }

        public MsgRecord SendCommand(ParamBase param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>()
            {
                { "ImgFileName", fileName },
                { "FileType", fileExtType },
                { "DeviceCode", deviceCode },
                { "DeviceType", deviceType },
                { "TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name } },
                { "OutputFileName", OutputFileName },
                { "IsInversion", false },
                { "BufferLen", BufferLen },
                { "Color", 1 },
                { "Channel", 1 }
            };

            if (TemplatePoiSelectedIndex > -1 && TemplatePoiSelectedIndex < TemplatePoi.Params.Count)
            {
                var poi = TemplatePoi.Params[TemplatePoiSelectedIndex].Value;
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = poi.Id, Name = poi.Name });
            }
            else
            {
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = -1, Name = null });
            }

            MsgSend msg = new()
            {
                EventName = "ARVR.DetectScreenDefects",
                SerialNumber = string.Empty,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
