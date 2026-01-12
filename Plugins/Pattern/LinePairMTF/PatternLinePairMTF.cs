using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Pattern.LinePairMTF
{
    public enum ChartType
    {
        FourLinePair,
        RotatedRect, // 斜方块
        BMW           // 宝马图
    }


    public class PatternLinePairMTFConfig : ViewModelBase, IConfig
    {
        public SolidColorBrush LineBrush { get => _LineBrush; set { _LineBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _LineBrush = Brushes.Black;

        public SolidColorBrush BackgroundBrush { get => _BackgroundBrush; set { _BackgroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackgroundBrush = Brushes.Green;

        [DisplayName("图像类型")]
        public ChartType ChartType { get => _ChartType; set { _ChartType = value; OnPropertyChanged(); } }
        private ChartType _ChartType = ChartType.FourLinePair;

        public int LineThickness { get => _LineThickness; set { _LineThickness = value; OnPropertyChanged(); } }
        private int _LineThickness = 2;

        public int LineLength { get => _LineLength; set { _LineLength = value; OnPropertyChanged(); } }
        private int _LineLength = 40;
        public double Angle { get => _Angle; set { _Angle = value; OnPropertyChanged(); } }
        private double _Angle = 45.0; // 默认45度

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<double> FieldX { get => _FieldX; set{ _FieldX = value; OnPropertyChanged(); } }
        private List<double> _FieldX = new List<double> { 0, 0.5, 0.8 };


        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<double> FieldY { get => _FieldY; set { _FieldY = value; OnPropertyChanged(); } }
        private List<double> _FieldY = new List<double> { 0, 0.5, 0.8 };


        public string LineBrushTag { get => _LineBrushTag; set { _LineBrushTag = value; OnPropertyChanged(); } }
        private string _LineBrushTag = "K";
        
        public string BackgroundBrushTag { get => _BackgroundBrushTag; set { _BackgroundBrushTag = value; OnPropertyChanged(); } }
        private string _BackgroundBrushTag = "W";

        [DisplayName("尺寸模式")]
        public PatternSizeMode SizeMode { get => _SizeMode; set { _SizeMode = value; OnPropertyChanged(); } }
        private PatternSizeMode _SizeMode = PatternSizeMode.ByFieldOfView;

        [PropertyVisibility(nameof(SizeMode), PatternSizeMode.ByFieldOfView)]
        [DisplayName("视场系数X")]
        public double FieldOfViewX { get => _FieldOfViewX; set { _FieldOfViewX = value; OnPropertyChanged(); } }
        private double _FieldOfViewX = 1.0;

        [PropertyVisibility(nameof(SizeMode), PatternSizeMode.ByFieldOfView)]
        [DisplayName("视场系数Y")]
        public double FieldOfViewY { get => _FieldOfViewY; set { _FieldOfViewY = value; OnPropertyChanged(); } }
        private double _FieldOfViewY = 1.0;

        [PropertyVisibility(nameof(SizeMode), PatternSizeMode.ByPixelSize)]
        [DisplayName("像素宽度")]
        public int PixelWidth { get => _PixelWidth; set { _PixelWidth = value; OnPropertyChanged(); } }
        private int _PixelWidth = 100;
        [PropertyVisibility(nameof(SizeMode), PatternSizeMode.ByPixelSize)]
        [DisplayName("像素高度")]
        public int PixelHeight { get => _PixelHeight; set { _PixelHeight = value; OnPropertyChanged(); } }
        private int _PixelHeight = 100;


    }

    [DisplayName("MTF")]
    public class PatternLinePairMTF : IPatternBase<PatternLinePairMTFConfig>
    {
        public override UserControl GetPatternEditor() => new LinePairMTFEditor(Config);

        public override string GetTemplateName()
        {
            string baseName = Config.ChartType.ToString() + "_" + Config.LineBrushTag + Config.BackgroundBrushTag +
                $"_{Config.LineThickness}_{Config.LineLength}";
            
            // Add FOV/Pixel suffix
            if (Config.SizeMode == PatternSizeMode.ByPixelSize)
            {
                baseName += $"_Pixel_{Config.PixelWidth}x{Config.PixelHeight}";
            }
            else // ByFieldOfView
            {
                // Only add suffix if not full FOV
                if (Config.FieldOfViewX != 1.0 || Config.FieldOfViewY != 1.0)
                {
                    baseName += $"_FOV_{Config.FieldOfViewX:0.##}x{Config.FieldOfViewY:0.##}";
                }
            }
            
            return baseName;
        }

        public override Mat Gen(int height, int width)
        {
            int fovWidth, fovHeight;

            // Calculate dimensions based on size mode
            if (Config.SizeMode == PatternSizeMode.ByPixelSize)
            {
                // Use pixel-based dimensions
                fovWidth = Math.Min(Config.PixelWidth, width);
                fovHeight = Math.Min(Config.PixelHeight, height);
            }
            else // ByFieldOfView
            {
                // Use field-of-view coefficients
                double fovx = Math.Max(0, Math.Min(Config.FieldOfViewX, 1.0));
                double fovy = Math.Max(0, Math.Min(Config.FieldOfViewY, 1.0));

                fovWidth = (int)(width * fovx);
                fovHeight = (int)(height * fovy);
            }

            // If dimensions match the entire image, generate pattern directly
            if (fovWidth == width && fovHeight == height)
            {
                Mat checker = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.BackgroundBrush.ToScalar());

            int cent_x = fovWidth / 2;
            int cent_y = fovHeight / 2;

            var fieldX = Config.FieldX;
            var fieldY = Config.FieldY;
            int count = Math.Min(fieldX.Count, fieldY.Count);

            for (int i = 0; i < count; i++)
            {
                double fx = fieldX[i];
                double fy = fieldY[i];

                int x1 = (int)(cent_x - cent_x * fx);
                int y1 = (int)(cent_y - cent_y * fy);
                int x2 = (int)(cent_x + cent_x * fx);
                int y2 = (int)(cent_y - cent_y * fy);
                int x3 = (int)(cent_x - cent_x * fx);
                int y3 = (int)(cent_y + cent_y * fy);
                int x4 = (int)(cent_x + cent_x * fx);
                int y4 = (int)(cent_y + cent_y * fy);
                if (Config.ChartType == ChartType.FourLinePair)
                {
                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x1, y1);

                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x2, y2);

                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x3, y3);

                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x4, y4);

                }
                else if (Config.ChartType == ChartType.RotatedRect)
                {
                    // 以RotatedRect为例
                    DrawRotatedRect(checker, x1, y1, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                    DrawRotatedRect(checker, x2, y2, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                    DrawRotatedRect(checker, x3, y3, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                    DrawRotatedRect(checker, x4, y4, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                }
                else if (Config.ChartType == ChartType.BMW)
                {
                    DrawBMW(checker, x1, y1, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                    DrawBMW(checker, x2, y2, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                    DrawBMW(checker, x3, y3, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                    DrawBMW(checker, x4, y4, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                }
            }

            return checker;
        }
        else
        {
            // Create background mat
            var mat = new Mat(height, width, MatType.CV_8UC3, Config.BackgroundBrush.ToScalar());
            int startX = (width - fovWidth) / 2;
            int startY = (height - fovHeight) / 2;

            Mat checker = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.BackgroundBrush.ToScalar());

            int cent_x = fovWidth / 2;
            int cent_y = fovHeight / 2;

            var fieldX = Config.FieldX;
            var fieldY = Config.FieldY;
            int count = Math.Min(fieldX.Count, fieldY.Count);

            for (int i = 0; i < count; i++)
            {
                double fx = fieldX[i];
                double fy = fieldY[i];

                int x1 = (int)(cent_x - cent_x * fx);
                int y1 = (int)(cent_y - cent_y * fy);
                int x2 = (int)(cent_x + cent_x * fx);
                int y2 = (int)(cent_y - cent_y * fy);
                int x3 = (int)(cent_x - cent_x * fx);
                int y3 = (int)(cent_y + cent_y * fy);
                int x4 = (int)(cent_x + cent_x * fx);
                int y4 = (int)(cent_y + cent_y * fy);
                if (Config.ChartType == ChartType.FourLinePair)
                {
                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x1, y1);

                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x2, y2);

                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x3, y3);

                    DrawFourLinePair(checker, fovWidth, fovHeight, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x4, y4);

                }
                else if (Config.ChartType == ChartType.RotatedRect)
                {
                    // 以RotatedRect为例
                    DrawRotatedRect(checker, x1, y1, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                    DrawRotatedRect(checker, x2, y2, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                    DrawRotatedRect(checker, x3, y3, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                    DrawRotatedRect(checker, x4, y4, Config.LineLength, Config.LineThickness, Config.Angle, Config.LineBrush.ToScalar());
                }
                else if (Config.ChartType == ChartType.BMW)
                {
                    DrawBMW(checker, x1, y1, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                    DrawBMW(checker, x2, y2, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                    DrawBMW(checker, x3, y3, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                    DrawBMW(checker, x4, y4, Config.LineLength, Config.Angle, Config.LineBrush.ToScalar());
                }
            }
            // 4. 贴到底图中心
            checker.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
            checker.Dispose();
            return mat;
        }
        }

        // 斜方块
        private static void DrawRotatedRect(Mat mat, int cx, int cy, int size, int thickness, double angle, Scalar color)
        {
            var rect = new RotatedRect(new Point2f(cx, cy), new Size2f(size, size), (float)angle);
            Point2f[] vertices = rect.Points();
            Point[] pts = Array.ConvertAll(vertices, v => new Point((int)v.X, (int)v.Y));
            Cv2.FillConvexPoly(mat, pts, color);
        }

        // 宝马图
        private static void DrawBMW(Mat mat, int cx, int cy, int size, double angle, Scalar color)
        {
            // 画两个对称的扇形
            Cv2.Ellipse(mat, new Point(cx, cy), new Size(size, size), angle, 0, 90, color, -1, LineTypes.AntiAlias);
            Cv2.Ellipse(mat, new Point(cx, cy), new Size(size, size), angle, 180, 270, color, -1, LineTypes.AntiAlias);
        }

        // 绘制指定位置的四线对
        private static void DrawFourLinePair(Mat mat, int width, int height, int lineLength, int lineThickness, Scalar lineColor, int xpoint, int ypoint)
        {
            for (int i = 0; i < lineLength; i++)
            {
                for (int j = 0; j < lineLength; j++)
                {
                    // 横线对
                    if ((i / lineThickness) % 2 == 0)
                    {
                        SetPixelSafe(mat, ypoint - lineLength + i, xpoint - lineLength + j, lineColor); // 左上
                        SetPixelSafe(mat, ypoint + i, xpoint + j, lineColor); // 右下
                    }
                    // 竖线对
                    if ((j / lineThickness) % 2 == 0)
                    {
                        SetPixelSafe(mat, ypoint - lineLength + i, xpoint + j, lineColor); // 右上
                        SetPixelSafe(mat, ypoint + i, xpoint - lineLength + j, lineColor); // 左下
                    }
                }
            }
        }

        // 边界安全设置像素
        private static void SetPixelSafe(Mat mat, int y, int x, Scalar color)
        {
            if (x >= 0 && x < mat.Width && y >= 0 && y < mat.Height)
                mat.Set<Vec3b>(y, x, new Vec3b((byte)color.Val0, (byte)color.Val1, (byte)color.Val2));
        }
    }

}
