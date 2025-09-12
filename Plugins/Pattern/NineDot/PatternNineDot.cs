using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.NineDot
{
    public class PatternNineDotConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;


        public int Radius { get => _Radius; set { _Radius = value; OnPropertyChanged(); } }
        private int _Radius = 50;

        public int StartX { get => _StartX; set { _StartX = value; OnPropertyChanged(); } }
        private int _StartX = -1;

        public int StartY { get => _StartY; set { _StartY = value; OnPropertyChanged(); } }
        private int _StartY = -1;

        public int Cols { get => _Cols; set { _Cols = value; OnPropertyChanged(); } }
        private int _Cols =3;

        public int Rows { get => _Rows; set { _Rows = value; OnPropertyChanged(); } }
        private int _Rows = 3;
    }

    [DisplayName("九点")]
    public class PatternNineDot : IPatternBase<PatternNineDotConfig>
    {
        public override UserControl GetPatternEditor() => new NineDotEditor(Config);
        public override string GetTemplateName()
        {
            return "NineDot" + "_" + DateTime.Now.ToString("HHmmss");
        }
        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            int radius = Config.Radius;
            // 3x3点阵，间隔算法
            int rows = Config.Rows, cols = Config.Cols;
            double gapX = (width - cols * radius) / (cols + 1.0);
            double gapY = (height - rows * radius) / (rows + 1.0);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int cx = (int)(gapX + radius / 2 + j * (radius + gapX));
                    int cy = (int)(gapY + radius / 2 + i * (radius + gapY));
                    Cv2.Circle(mat, new Point(cx, cy), radius / 2, Config.AltBrush.ToScalar(), -1, LineTypes.AntiAlias);
                }
            }
            return mat;
        }
    }
}
