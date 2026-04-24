using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public class Window3DConfig : ViewModelBase, IConfig
    {
        public static Window3DConfig Instance => ConfigService.Instance.GetRequiredService<Window3DConfig>();

        public int TargetPixelsX { get => _TargetPixelsX; set { _TargetPixelsX = value; OnPropertyChanged(); } }
        private int _TargetPixelsX = 512;

        public int TargetPixelsY { get => _TargetPixelsY; set { _TargetPixelsY = value; OnPropertyChanged(); } }
        private int _TargetPixelsY = 512;

        public string SelectedColormap { get => _SelectedColormap; set { _SelectedColormap = value; OnPropertyChanged(); } }
        private string _SelectedColormap = "jet";
    }

    public record ColormapInfo(string Name, BitmapImage? ImageSource, byte[]? Lut);
}
