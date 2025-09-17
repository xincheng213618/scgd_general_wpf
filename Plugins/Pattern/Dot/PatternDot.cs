using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Dot
{
    public class PatternDotConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.Green;

        public int Spacing { get => _Spacing; set { _Spacing = value; OnPropertyChanged(); } }
        private int _Spacing = 20;
        public int Radius { get => _Radius; set { _Radius = value; OnPropertyChanged(); } }
        private int _Radius = 3;


        /// <summary>
        /// 行数，-1表示自适应
        /// </summary>
        public int Rows { get => _Rows; set { _Rows = value; OnPropertyChanged(); } }
        private int _Rows = -1;
        /// <summary>
        /// 列数，-1表示自适应
        /// </summary>
        public int Cols { get => _Cols; set { _Cols = value; OnPropertyChanged(); } }
        private int _Cols = -1;



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

        public double FieldOfViewX { get => _FieldOfViewX; set { _FieldOfViewX = value; OnPropertyChanged(); } }
        private double _FieldOfViewX = 1.0;
        public double FieldOfViewY { get => _FieldOfViewY; set { _FieldOfViewY = value; OnPropertyChanged(); } }
        private double _FieldOfViewY = 1.0;

    }

    [DisplayName("点阵")]
    public class PatternDot : IPatternBase<PatternDotConfig>
    {
        public override UserControl GetPatternEditor() => new DotEditor(Config);

        public override string GetTemplateName()
        {
            var shape = !Config.UseRectangle ? "Circle" : "Rect";
            var size = !Config.UseRectangle ? Config.Radius.ToString() : $"{Config.RectWidth}x{Config.RectHeight}";
            return $"DotMatrix_{shape}_{size}";
        }
        public override Mat Gen(int height, int width)
        {
            // 创建底图
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            // 2. 计算视场中心区域
            double fovx = Math.Max(0, Math.Min(Config.FieldOfViewX, 1.0));
            double fovy = Math.Max(0, Math.Min(Config.FieldOfViewY, 1.0));

            int fovWidth = (int)(width * fovx);
            int fovHeight = (int)(height * fovy);
            int startX = (width - fovWidth) / 2;
            int startY = (height - fovHeight) / 2;

            // 点阵区域小图
            Mat dots = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            int spacing = Math.Max(1, Config.Spacing);
            int radius = Math.Max(1, Config.Radius);

            // 行列自适应
            int nCol = Config.Cols > 0 ? Config.Cols : (int)Math.Ceiling((double)fovWidth / spacing);
            int nRow = Config.Rows > 0 ? Config.Rows : (int)Math.Ceiling((double)fovHeight / spacing);

            Scalar color = Config.AltBrush.ToScalar();
            bool useRect = Config.UseRectangle;
            int rectW = Config.RectWidth > 0 ? Config.RectWidth : Math.Max(1, 2 * radius);
            int rectH = Config.RectHeight > 0 ? Config.RectHeight : Math.Max(1, 2 * radius);

            for (int i = 0; i < nRow; i++)
            {
                int y = spacing / 2 + i * spacing;
                if (y >= fovHeight) break;
                for (int j = 0; j < nCol; j++)
                {
                    int x = spacing / 2 + j * spacing;
                    if (x >= fovWidth) break;

                    if (!useRect)
                    {
                        // 画实心圆
                        Cv2.Circle(dots, new OpenCvSharp.Point(x, y), radius, color, -1);
                    }
                    else
                    {
                        // 以 (x,y) 为中心的实心矩形
                        int x0 = x - rectW / 2;
                        int y0 = y - rectH / 2;

                        int xClip = Math.Max(0, x0);
                        int yClip = Math.Max(0, y0);
                        int wClip = Math.Min(rectW, fovWidth - xClip);
                        int hClip = Math.Min(rectH, fovHeight - yClip);

                        if (wClip > 0 && hClip > 0)
                        {
                            Cv2.Rectangle(dots, new Rect(xClip, yClip, wClip, hClip), color, -1);
                        }
                    }
                }
            }

            // 合成到主图中心
            dots.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
            dots.Dispose();
            return mat;
        }
    }
}
