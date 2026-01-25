using ColorVision.Common.MVVM;
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
    [DisplayAlgorithm(20, "PixelLedDetect", "定位算法")]
    public class AlgorithmLedCheck : DisplayAlgorithmBase
    {

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
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateLedCheck(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
        public int TemplatePoiSelectedIndex { get => _TemplatePoiSelectedIndex; set { _TemplatePoiSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplatePoiSelectedIndex;
        public void OpenTemplatePoi()
        {
            new TemplateEditorWindow(new TemplatePoi(), TemplatePoiSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }




        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayLedCheck(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public MsgRecord SendCommand(LedCheckParam param, PoiParam poiParam ,string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            // 序列号生成优化
            string sn = string.IsNullOrWhiteSpace(serialNumber) ? DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff"): serialNumber;

            // 文件路径处理优化
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullPath))
            {
                fileName = fullPath;
            }

            // 组装参数，使用集合初始化器
            var Params = new Dictionary<string, object>
            {
                ["ImgFileName"] = fileName,
                ["FileType"] = fileExtType,
                ["DeviceCode"] = deviceCode,
                ["DeviceType"] = deviceType,
                ["TemplateParam"] = new CVTemplateParam { ID = param.Id, Name = param.Name },
                ["POITemplateParam"] = new CVTemplateParam
                {
                    ID = poiParam.Id,
                    Name = poiParam.Id == -1 ? string.Empty : poiParam.Name
                }
            };

            var msg = new MsgSend
            {
                EventName = MQTTAlgorithmEventEnum.Event_LED_Check_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
