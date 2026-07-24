using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class AlgorithmKBConfig : ViewModelBase, IConfig
    {
        public static AlgorithmKBConfig Instance =>ConfigService.Instance.GetRequiredService<AlgorithmKBConfig>();
    }

    public class KBDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [CommandDisplay("编辑键盘点位")]
        public ICommand EditFirstTemplateCommand { get; }

        public KBDisplayAlgorithmConfig()
            : base(new DisplayAlgorithmTemplateSelection(
                "键盘检测模板",
                new TemplateKB(),
                "请先选择键盘检测模板"))
        {
            EditFirstTemplateCommand = new RelayCommand(_ => OpenFirstTemplate());
        }

        private void OpenFirstTemplate()
        {
            if (Template.TryGetValue(out TemplateJsonKBParam param))
            {
                new EditPoiParam1(param)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.Show();
            }
        }
    }

    [DisplayAlgorithm(98, "键盘检测1", "数据提取算法")]
    public class AlgorithmKB : JsonDisplayAlgorithmBase<KBDisplayAlgorithmConfig>
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public AlgorithmKB(DeviceAlgorithm deviceAlgorithm)
            : base(new KBDisplayAlgorithmConfig())
        {
			Device = deviceAlgorithm;
        }

        public override MsgRecord SendCommand(TemplateJsonParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType)
        {
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new Dictionary<string,object>() { { "ID", param.Id },{ "Name", param.Name } });
            MsgSend msg = new()
            {
                EventName = "KB",
                SerialNumber = string.Empty,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }

    }
}
