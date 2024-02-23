using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Devices.Camera;
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
            View ??= new AlgorithmView(this);
            MQTTService = new MQTTAlgorithm(this, Config);

            if (Application.Current.TryFindResource("DrawingImageAlgorithm") is DrawingImage DrawingImageAlgorithm)
                Icon = DrawingImageAlgorithm;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("DrawingImageAlgorithm") is DrawingImage DrawingImageAlgorithm)
                    Icon = DrawingImageAlgorithm;
                View.View.Icon = Icon;
            };
            View.View.Title = "算法展示";
            View.View.Icon = Icon;

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

    }
}
