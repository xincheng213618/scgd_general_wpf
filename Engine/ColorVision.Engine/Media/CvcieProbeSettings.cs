using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieProbeSettings : ViewModelBase
    {
        public double Radius { get => _radius; set { _radius = value; OnPropertyChanged(); } }
        private double _radius = 100;

        public int RectWidth { get => _rectWidth; set { _rectWidth = value; OnPropertyChanged(); } }
        private int _rectWidth = 120;

        public int RectHeight { get => _rectHeight; set { _rectHeight = value; OnPropertyChanged(); } }
        private int _rectHeight = 120;

        public MagnigifierType MagnigifierType { get => _magnigifierType; set { _magnigifierType = value; OnPropertyChanged(); } }
        private MagnigifierType _magnigifierType = MagnigifierType.Circle;
    }
}
