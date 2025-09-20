using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.SFR
{
    public class PatternSFRConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.White;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.Black;

        public int Cols { get => _Cols; set { _Cols = value; OnPropertyChanged(); } }
        private int _Cols = 3;

        public int Rows { get => _Rows; set { _Rows = value; OnPropertyChanged(); } }
        private int _Rows = 3;

        public int SquareSize { get => _SquareSize; set { _SquareSize = value; OnPropertyChanged(); } }
        private int _SquareSize = 50;

        public double AngleDeg { get => _AngleDeg; set { _AngleDeg = value; OnPropertyChanged(); } }
        private double _AngleDeg = 10.0;

        public double BorderRatio { get => _BorderRatio; set { _BorderRatio = value; OnPropertyChanged(); } }
        private double _BorderRatio = 0.1;

        public double FieldOfViewX { get => _FieldOfViewX; set { _FieldOfViewX = value; OnPropertyChanged(); } }
        private double _FieldOfViewX = 1.0;
        public double FieldOfViewY { get => _FieldOfViewY; set { _FieldOfViewY = value; OnPropertyChanged(); } }
        private double _FieldOfViewY = 1.0;

    }

    [DisplayName("SFR")]
    public class PatternSFR : IPatternBase<PatternSFRConfig>
    {
        public override UserControl GetPatternEditor() => new SFREditor(Config);
        public override string GetTemplateName()
        {
            return "SFR" + "_" + DateTime.Now.ToString("HHmmss");
        }
        public override Mat Gen(int height, int width)
        {
            int cols = Config.Cols;
            int rows = Config.Rows;
            int squareSize = Config.SquareSize;
            double angleDeg = Config.AngleDeg;
            double borderRatio = Config.BorderRatio;
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
            Mat sfr = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            
            double leftRightMargin = fovWidth * borderRatio;
            double topBottomMargin = fovHeight * borderRatio;
            double usableWidth = fovWidth - 2 * leftRightMargin;
            double usableHeight = fovHeight - 2 * topBottomMargin;
            double xSpacing = (cols == 1) ? 0 : usableWidth / (cols - 1);
            double ySpacing = (rows == 1) ? 0 : usableHeight / (rows - 1);

            var centers = new List<Point2f>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    float cx = (float)(leftRightMargin + col * xSpacing);
                    float cy = (float)(topBottomMargin + row * ySpacing);
                    centers.Add(new Point2f(cx, cy));
                }
            }

            double theta = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(theta);
            double sin = Math.Sin(theta);
            float half = squareSize / 2f;

            // 正方形顶点
            Point2f[] baseVertices = new[]
            {
            new Point2f(-half, -half),
            new Point2f( half, -half),
            new Point2f( half,  half),
            new Point2f(-half,  half)
        };
            foreach (var center in centers)
            {
                // 旋转
                Point[] pts = new Point[4];
                for (int i = 0; i < 4; i++)
                {
                    float x = baseVertices[i].X;
                    float y = baseVertices[i].Y;
                    float rx = (float)(x * cos - y * sin) + center.X;
                    float ry = (float)(x * sin + y * cos) + center.Y;
                    pts[i] = new Point((int)Math.Round(rx), (int)Math.Round(ry));
                }
                Cv2.FillPoly(sfr, new[] { pts }, Config.AltBrush.ToScalar());
            }

            // 合成到主图中心
            sfr.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
            sfr.Dispose();
            return mat;
        }
    }
}
