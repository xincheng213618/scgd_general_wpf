using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public class Window3DConfig : ViewModelBase, IConfig
    {
        public static Window3DConfig Instance => ConfigService.Instance.GetRequiredService<Window3DConfig>();

        public double DefaultHeightScale { get => _DefaultHeightScale; set { _DefaultHeightScale = value < 1 ? 1 : value; OnPropertyChanged(); } }
        private double _DefaultHeightScale = 100;

        public int TargetPixelsX { get => _TargetPixelsX; set { _TargetPixelsX = value; OnPropertyChanged(); } }
        private int _TargetPixelsX = 512;

        public int TargetPixelsY { get => _TargetPixelsY; set { _TargetPixelsY = value; OnPropertyChanged(); } }
        private int _TargetPixelsY = 512;

        // Existing X/Y preferences now bound the cached interaction mesh. Detail has an
        // independent budget, so an older saved 512 preference still gains a detailed view.
        public int DetailResolution { get => _DetailResolution; set { _DetailResolution = System.Math.Clamp(value, 128, 2048); OnPropertyChanged(); } }
        private int _DetailResolution = 1536;

        public bool AdaptiveDetail { get => _AdaptiveDetail; set { _AdaptiveDetail = value; OnPropertyChanged(); } }
        private bool _AdaptiveDetail = true;

        public string SelectedColormap { get => _SelectedColormap; set { _SelectedColormap = value; OnPropertyChanged(); } }
        private string _SelectedColormap = "jet";
    }

    public record ColormapInfo(string Name, BitmapImage? ImageSource, byte[]? Lut);
}
