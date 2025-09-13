using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.Util.Draw.Rectangle;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.NineDot
{
    // 新增枚举
    public enum DotFitType
    {
        Center,         // 居中原样
        Inscribed,      // 内切
        Circumscribed   // 外切
    }

    public class PatternNineDotConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;

        public int Radius { get => _Radius; set { _Radius = value; OnPropertyChanged(); } }
        private int _Radius = 30;

        public int Cols { get => _Cols; set { _Cols = value; OnPropertyChanged(); } }
        private int _Cols =3;

        public int Rows { get => _Rows; set { _Rows = value; OnPropertyChanged(); } }
        private int _Rows = 3;
        public DotFitType DotFitType { get => _DotFitType; set { _DotFitType = value; OnPropertyChanged(); } }
        private DotFitType _DotFitType = DotFitType.Center;

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


        public double MarginRatioX
        {
            get => _MarginRatioX;
            set { _MarginRatioX = value; OnPropertyChanged(); }
        }
        private double _MarginRatioX = 0.1;

        public double MarginRatioY
        {
            get => _MarginRatioY;
            set { _MarginRatioY = value; OnPropertyChanged(); }
        }
        private double _MarginRatioY = 0.1;

        public string MainBrushTag { get; set; } = "K";
        public string AltBrushTag { get; set; } = "W";
    }

    [DisplayName("九点")]
    public class PatternNineDot : IPatternBase<PatternNineDotConfig>
    {
        public override UserControl GetPatternEditor() => new NineDotEditor(Config);
        public override string GetTemplateName()
        {
            string shape = !Config.UseRectangle
                ? $"Circle_{Config.Radius}"
                : $"Rect_{Config.RectWidth}x{Config.RectHeight}";
            return $"Distortion_{Config.MainBrushTag}{Config.AltBrushTag}_{Config.Rows}x{Config.Cols}_{shape}";
        }
        public static List<Point> GetBilinearGridPoints(List<Point> quad, int rows, int cols)
        {
            var result = new List<Point>();
            double rowStep = (rows > 1) ? 1.0 / (rows - 1) : 0;
            double colStep = (cols > 1) ? 1.0 / (cols - 1) : 0;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double u = i * rowStep;
                    double v = j * colStep;

                    double x = (1 - u) * (1 - v) * quad[0].X + (1 - u) * v * quad[1].X + u * v * quad[2].X + u * (1 - v) * quad[3].X;
                    double y = (1 - u) * (1 - v) * quad[0].Y + (1 - u) * v * quad[1].Y + u * v * quad[2].Y + u * (1 - v) * quad[3].Y;

                    result.Add(new Point(Math.Round(x), Math.Round(y)));
                }
            }
            return result;
        }
        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            Scalar color = Config.AltBrush.ToScalar();
            int rectW = Config.RectWidth;
            int rectH = Config.RectHeight;

            // 支持比例和绝对值
            double marginX = Config.MarginRatioX < 1 ? width * Config.MarginRatioX : Config.MarginRatioX;
            double marginY = Config.MarginRatioY < 1 ? height * Config.MarginRatioY : Config.MarginRatioY;

            List<Point> quad;
            if (Config.DotFitType == DotFitType.Center)
            {
                quad = new List<Point>
        {
            new Point(marginX, marginY),
            new Point(width - marginX, marginY),
            new Point(width - marginX, height - marginY),
            new Point(marginX, height - marginY)
        };
            }
            else
            {
                double offsetX = Config.UseRectangle ? rectW / 2.0 : Config.Radius;
                double offsetY = Config.UseRectangle ? rectH / 2.0 : Config.Radius;
                if (Config.DotFitType == DotFitType.Inscribed)
                {
                    quad = new List<Point>
            {
                new Point(marginX + offsetX, marginY + offsetY),
                new Point(width - marginX - offsetX, marginY + offsetY),
                new Point(width - marginX - offsetX, height - marginY - offsetY),
                new Point(marginX + offsetX, height - marginY - offsetY)
            };
                }
                else // Circumscribed
                {
                    quad = new List<Point>
            {
                new Point(marginX - offsetX, marginY - offsetY),
                new Point(width - marginX + offsetX, marginY - offsetY),
                new Point(width - marginX + offsetX, height - marginY + offsetY),
                new Point(marginX - offsetX, height - marginY + offsetY)
            };
                }
            }

            var gridPoints = GetBilinearGridPoints(quad, Config.Rows, Config.Cols);

            foreach (var pt in gridPoints)
            {
                if (!Config.UseRectangle)
                    Cv2.Circle(mat, pt, Config.Radius, color, -1);
                else
                    Cv2.Rectangle(mat, new Rect(pt.X - rectW / 2, pt.Y - rectH / 2, rectW, rectH), color, -1);
            }
            return mat;

        }

    }
}
