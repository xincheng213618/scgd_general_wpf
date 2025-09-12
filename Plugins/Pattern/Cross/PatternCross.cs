using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Cross
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

        public string MainBrushTag { get; set; } = "K";
        public string AltBrushTag { get; set; } = "W";
    }

    [DisplayName("十字")]
    public class PatternCross : IPatternBase<PatternCrossConfig>
    {
        public override UserControl GetPatternEditor() => new CrossEditor(Config);
        public override string GetTemplateName()
        {
            return "Cross" + "_" + Config.MainBrushTag + Config.AltBrushTag + $"_{Config.HorizontalWidth}*{Config.VerticalWidth}";
        }
        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            // 横线（中心线）
            int hCenter = height / 2;
            int hStart = Math.Max(0, hCenter - Config.HorizontalWidth / 2);
            int hEnd = Math.Min(height, hCenter + (Config.HorizontalWidth + 1) / 2);
            for (int y = hStart; y < hEnd; y++)
            {
                mat.Row(y).SetTo(Config.AltBrush.ToScalar());
            }

            // 竖线（中心线）
            int vCenter = width / 2;
            int vStart = Math.Max(0, vCenter - Config.VerticalWidth / 2);
            int vEnd = Math.Min(width, vCenter + (Config.VerticalWidth + 1) / 2);
            for (int x = vStart; x < vEnd; x++)
            {
                mat.Col(x).SetTo(Config.AltBrush.ToScalar());
            }
            return mat;
        }
    }
}
