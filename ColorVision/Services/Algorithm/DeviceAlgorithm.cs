using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Algorithm
{
    public class DeviceAlgorithm : BaseDevice<ConfigAlgorithm>
    {
        public AlgorithmService Service { get; set; }
        public AlgorithmView View { get; set; }

        public DeviceAlgorithm(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            View ??= new AlgorithmView();
            Service = new AlgorithmService(this,Config);

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
        }

        public override UserControl GetDeviceControl() => new DeviceAlgorithmControl(this);
        public override UserControl GetDeviceInfo() => new DeviceAlgorithmControl(this,false);

        public override UserControl GetDisplayControl() => new DisplayAlgorithmControl(this);
    }
}
