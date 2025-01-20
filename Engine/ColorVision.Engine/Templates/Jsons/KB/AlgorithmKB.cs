using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.UI;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class AlgorithmKBConfig : ViewModelBase, IConfig
    {
        public static AlgorithmKBConfig Instance =>ConfigService.Instance.GetRequiredService<AlgorithmKBConfig>();
    }

    public class AlgorithmKB : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "KB服务测试";

        public int Order { get; set; } = 98;

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

     
        public AlgorithmKB(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateKB(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayKB(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            ParamBase paramBase = TemplateKB.Params[TemplateSelectedIndex].Value;
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new Dictionary<string,object>() { { "ID", paramBase.Id },{ "Name", paramBase.Name } });
            MsgSend msg = new()
            {
                EventName = "KB",
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }

    }
}
