using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Ring
{
    public class PatternRingConfig:ViewModelBase,IConfig
    {

        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;

        public int RingWidth { get => _RingWidth; set { _RingWidth = value; OnPropertyChanged(); } }
        private int _RingWidth = 30;

        public List<int> RingOffsets { get => _RingOffsets; set { _RingOffsets = value; OnPropertyChanged(); } }
        private List<int> _RingOffsets = new List<int> { 0, 100, 200, 400 };

        [DisplayName("是否绘制中心线")]
        public bool DrawCenterLine { get => _DrawCenterLine; set { _DrawCenterLine = value; OnPropertyChanged(); } }
        private bool _DrawCenterLine = true;
    }

    [DisplayName("Ring")]
    public class PatternRing : IPatternBase<PatternRingConfig>
    {
        public override UserControl GetPatternEditor() => new RingEditor(Config);
        public override string GetTemplateName()
        {
            return "Ring" + "_" + DateTime.Now.ToString("HHmmss");
        }
        public override Mat Gen(int height, int width)
        {
            int ringWidth = Config.RingWidth;

            var img = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            double centerX = width / 2.0;
            double centerY = height / 2.0;
            double maxR = Math.Min(centerX, centerY) - ringWidth / 2.0;


            foreach (var offset in Config.RingOffsets)
            {
                double outerRadius = maxR - offset;
                double innerRadius = outerRadius - ringWidth;
                Cv2.Circle(img, new Point(centerX, centerY), (int)Math.Round((outerRadius + innerRadius) / 2), Config.AltBrush.ToScalar(), ringWidth, LineTypes.AntiAlias);
            }

            // 中心线
            if (Config.DrawCenterLine)
            {
                // 水平
                Cv2.Line(img, new Point(0, height / 2), new Point(width - 1, height / 2), Config.AltBrush.ToScalar(), 2);
                // 垂直
                Cv2.Line(img, new Point(width / 2, 0), new Point(width / 2, height - 1), Config.AltBrush.ToScalar(), 2);
            }
            return img;
        }
    }
}
