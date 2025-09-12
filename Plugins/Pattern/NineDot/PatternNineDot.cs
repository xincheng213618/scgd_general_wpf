using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using OpenCvSharp;
using SkiaSharp;
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

        public int Cols { get => _Cols; set { _Cols = value; OnPropertyChanged(); } }
        private int _Cols =3;

        public int Rows { get => _Rows; set { _Rows = value; OnPropertyChanged(); } }
        private int _Rows = 3;


        /// <summary>
        /// 是否使用矩形（为 false 时使用圆形）
        /// </summary>
        public bool UseRectangle { get => _UseRectangle; set { _UseRectangle = value; OnPropertyChanged(); } }
        private bool _UseRectangle;

        /// <summary>
        /// 矩形宽度（像素）
        /// </summary>
        public int RectWidth { get => _RectWidth; set { _RectWidth = value; OnPropertyChanged(); } }
        private int _RectWidth = 6;

        /// <summary>
        /// 矩形高度（像素）
        /// </summary>
        public int RectHeight { get => _RectHeight; set { _RectHeight = value; OnPropertyChanged(); } }
        private int _RectHeight = 6;
    }

    [DisplayName("九点")]
    public class PatternNineDot : IPatternBase<PatternNineDotConfig>
    {
        public override UserControl GetPatternEditor() => new NineDotEditor(Config);
        public override string GetTemplateName()
        {
            string shape = Config.UseRectangle
                ? $"Circle_{Config.Radius}"
                : $"Rect_{Config.RectWidth}*{Config.RectHeight}";
            return $"Distortion_{Config.Rows}*{Config.Cols}_{shape}";
        }

        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            int radius = Config.Radius;
            // 3x3点阵，间隔算法
            int rows = Config.Rows, cols = Config.Cols;
            double gapX = (width - cols * radius) / (cols + 1.0);
            double gapY = (height - rows * radius) / (rows + 1.0);

            Scalar color = Config.AltBrush.ToScalar();
            bool useRect = Config.UseRectangle;
            int rectW = Config.RectWidth > 0 ? Config.RectWidth : Math.Max(1, 2 * radius);
            int rectH = Config.RectHeight > 0 ? Config.RectHeight : Math.Max(1, 2 * radius);


            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int x = (int)(gapX + radius / 2 + j * (radius + gapX));
                    int y = (int)(gapY + radius / 2 + i * (radius + gapY));

                    if (!useRect)
                    {
                        // 画实心圆
                        Cv2.Circle(mat, new OpenCvSharp.Point(x, y), radius, color, -1);
                    }
                    else
                    {
                        // 以 (x,y) 为中心的实心矩形
                        int x0 = x - rectW / 2;
                        int y0 = y - rectH / 2;

                        int xClip = Math.Max(0, x0);
                        int yClip = Math.Max(0, y0);
                        int wClip = Math.Min(rectW, width - xClip);
                        int hClip = Math.Min(rectH, height - yClip);

                        if (wClip > 0 && hClip > 0)
                        {
                            Cv2.Rectangle(mat, new Rect(xClip, yClip, wClip, hClip), color, -1);
                        }
                    }
                }
            }
            return mat;
        }
    }
}
