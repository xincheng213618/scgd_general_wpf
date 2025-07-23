using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class AlgorithmKBConfig : ViewModelBase, IConfig,IConfigSettingProvider
    {
        public static AlgorithmKBConfig Instance =>ConfigService.Instance.GetRequiredService<AlgorithmKBConfig>();
        public double KBLVSacle { get => _KBLVSacle; set { _KBLVSacle = value; NotifyPropertyChanged(); } }
        private double _KBLVSacle = 0.006583904;

        public bool KBCanDrag { get => _KBCanDrag; set { _KBCanDrag = value; NotifyPropertyChanged(); } }
        private bool _KBCanDrag;

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "KBLVSacle",
                                Description = "KBLVSacle",
                                Order = 2,
                                Group ="Engine",
                                Type = ConfigSettingType.Text,
                                BindingName =nameof(KBLVSacle),
                                Source = Instance,
                            },
                             new ConfigSettingMetadata
                            {
                                Name = "KBCanDrag",
                                Description = "KBCanDrag",
                                Order = 2,
                                Group ="Engine",
                                Type = ConfigSettingType.Bool,
                                BindingName =nameof(KBCanDrag),
                                Source = Instance,
                            }
            };
        }
    }
    [DisplayAlgorithm(98, "键盘检测", "数据提取算法")]
    public class AlgorithmKB : DisplayAlgorithmBase
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public RelayCommand OpenFirstTemplateCommand { get; set; }

        public AlgorithmKB(DeviceAlgorithm deviceAlgorithm)
        {
			Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFirstTemplateCommand = new RelayCommand(a => OpenFirstTemplate());
        }
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateKB(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public void OpenFirstTemplate()
        {
            new EditPoiParam1(TemplateKB.Params[TemplateSelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public override UserControl GetUserControl()
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
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;
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
