using ColorVision.Common.MVVM;
using ColorVision.Engine.Pattern.Ring;
using ColorVision.UI;
using NPOI.SS.UserModel;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using static iText.Kernel.Pdf.Colorspace.PdfPattern;

namespace ColorVision.Engine.Pattern.NineDot
{
    public class PatternNineDotConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;


        public int Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
        private int _Radius = 50;

        public int StartX { get => _StartX; set { _StartX = value; NotifyPropertyChanged(); } }
        private int _StartX = -1;

        public int StartY { get => _StartY; set { _StartY = value; NotifyPropertyChanged(); } }
        private int _StartY = -1;

        public int Cols { get => _Cols; set { _Cols = value; NotifyPropertyChanged(); } }
        private int _Cols =3;

        public int Rows { get => _Rows; set { _Rows = value; NotifyPropertyChanged(); } }
        private int _Rows = 3;
    }

    [DisplayName("九点")]
    public class PatternNineDot : IPatternBase<PatternNineDotConfig>
    {
        public static PatternNineDotConfig Config => ConfigService.Instance.GetRequiredService<PatternNineDotConfig>();
        public override ViewModelBase GetConfig() => Config;
        public override UserControl GetPatternEditor() => new NineDotEditor();
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
