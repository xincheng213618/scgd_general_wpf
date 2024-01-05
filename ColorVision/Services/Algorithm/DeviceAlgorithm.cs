using ColorVision.Device.Camera;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm.Views;
using ColorVision.Services.Device;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Algorithm
{
    public class DeviceAlgorithm : BaseDevice<ConfigAlgorithm>
    {
        public AlgorithmService DeviceService { get; set; }
        public AlgorithmView View { get; set; }

        public DeviceAlgorithm(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            View ??= new AlgorithmView(this);
            DeviceService = new AlgorithmService(this,Config);

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

            DisplayAlgorithmControlLazy = new Lazy<DisplayAlgorithmControl>(() => { DisplayAlgorithmControl ??= new DisplayAlgorithmControl(this);return DisplayAlgorithmControl;   });
        }
        readonly Lazy<DisplayAlgorithmControl> DisplayAlgorithmControlLazy;
        public DisplayAlgorithmControl DisplayAlgorithmControl { get; set; }

        public override UserControl GetDeviceControl() => new DeviceAlgorithmControl(this);
        public override UserControl GetDeviceInfo() => new DeviceAlgorithmControl(this,false);

        public override UserControl GetDisplayControl() => DisplayAlgorithmControlLazy.Value;
    }
}
