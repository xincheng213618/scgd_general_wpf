using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Ghost
{
    [DisplayAlgorithm(54, "Ghost1.0", "ARVR")]
    public class AlgorithmGhost : DisplayAlgorithmBase
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmGhost(DeviceAlgorithm deviceAlgorithm)
        {
			Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateGhost(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;


        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayGhost(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        public CVOLEDCOLOR CVOLEDCOLOR { get => _CVOLEDCOLOR; set { _CVOLEDCOLOR = value; OnPropertyChanged(); } }
        private CVOLEDCOLOR _CVOLEDCOLOR;

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, GhostParam ghostParam, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };

            Params.Add("TemplateParam", new CVTemplateParam() { ID = ghostParam.Id, Name = ghostParam.Name });
            Params.Add("Color", CVOLEDCOLOR);

            MsgSend msg = new()
            {
                EventName = "Ghost",
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }

    }
}
