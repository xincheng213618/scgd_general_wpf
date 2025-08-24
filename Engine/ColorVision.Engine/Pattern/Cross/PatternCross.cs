using ColorVision.Common.MVVM;
using ColorVision.Engine.Pattern.CrossGrid;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern.Cross
{

    public class PatternCrossConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;


        public int HorizontalWidth { get => _HorizontalWidth; set { _HorizontalWidth = value; OnPropertyChanged(); } }
        private int _HorizontalWidth = 3;

        public int VerticalWidth { get => _VerticalWidth; set { _VerticalWidth = value; OnPropertyChanged(); } }
        private int _VerticalWidth = 3;
    }

    [DisplayName("十字")]
    public class PatternCross : IPatternBase<PatternCrossConfig>
    {
        public override UserControl GetPatternEditor() => new CrossEditor(Config);
        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            OpenCvSharp.Cv2.Line(mat, new OpenCvSharp.Point(0, height / 2), new OpenCvSharp.Point(width, height / 2), Config.AltBrush.ToScalar(), Config.HorizontalWidth);
            OpenCvSharp.Cv2.Line(mat, new OpenCvSharp.Point(width / 2, 0), new OpenCvSharp.Point(width / 2, height), Config.AltBrush.ToScalar(), Config.VerticalWidth);
            return mat;
        }
    }
}
