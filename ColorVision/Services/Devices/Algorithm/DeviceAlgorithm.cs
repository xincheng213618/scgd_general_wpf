using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Devices.Calibration.Views;
using ColorVision.Services.Extension;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Devices.Algorithm
{
    public class DeviceAlgorithm : DeviceService<ConfigAlgorithm>
    {
        public MQTTAlgorithm MQTTService { get; set; }
        public AlgorithmView View { get; set; }

        public DeviceAlgorithm(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            MQTTService = new MQTTAlgorithm(this, Config);

            View = new AlgorithmView(this);
            View.View.Title = $"算法视图 - {Config.Code}";
            this.SetResource("DrawingImageAlgorithm", View.View);

            DisplayAlgorithmControlLazy = new Lazy<DisplayAlgorithmControl>(() => { DisplayAlgorithmControl ??= new DisplayAlgorithmControl(this); return DisplayAlgorithmControl; });

            EditLazy = new Lazy<EditAlorithm>(() => { EditAlorithm ??= new EditAlorithm(); return EditAlorithm; });
        }
        readonly Lazy<DisplayAlgorithmControl> DisplayAlgorithmControlLazy;
        public DisplayAlgorithmControl DisplayAlgorithmControl { get; set; }

        public override UserControl GetDeviceControl() => new DeviceAlgorithmControl(this);
        public override UserControl GetDeviceInfo() => new DeviceAlgorithmControl(this, false);

        public override UserControl GetDisplayControl() => DisplayAlgorithmControlLazy.Value;

        readonly Lazy<EditAlorithm> EditLazy;
        public EditAlorithm EditAlorithm { get; set; }
        public override UserControl GetEditControl() => EditLazy.Value;

        public override MQTTServiceBase? GetMQTTService()
        {
            return MQTTService;
        }
    }
}
