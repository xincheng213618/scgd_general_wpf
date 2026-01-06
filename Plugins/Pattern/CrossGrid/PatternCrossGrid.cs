using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.CrossGrid
{

    public class PatternCrossGridConfig:ViewModelBase,IConfig
    {

        // 网格参数
        public int NumLinesHorizontal { get => _NumLinesHorizontal; set { _NumLinesHorizontal = value; OnPropertyChanged(); } }
        private int _NumLinesHorizontal = 3;

        public int NumLinesVertical { get => _NumLinesVertical; set { _NumLinesVertical = value; OnPropertyChanged(); } }
        private int _NumLinesVertical = 3;

        public int SpacingHorizontal { get => _SpacingHorizontal; set { _SpacingHorizontal = value; OnPropertyChanged(); } }
        private int _SpacingHorizontal = 400;

        public int SpacingVertical { get => _SpacingVertical; set { _SpacingVertical = value; OnPropertyChanged(); } }
        private int _SpacingVertical = 400;

        public int HorizontalLineWidth { get => _HorizontalLineWidth; set { _HorizontalLineWidth = value; OnPropertyChanged(); } }
        private int _HorizontalLineWidth = 9;

        public int VerticalLineWidth { get => _VerticalLineWidth; set { _VerticalLineWidth = value; OnPropertyChanged(); } }
        private int _VerticalLineWidth = 9;


        public SolidColorBrush LineBrush { get => _LineBrush; set { _LineBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _LineBrush = Brushes.White;

        public SolidColorBrush BackgroundBrush { get => _BackgroundBrush; set { _BackgroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackgroundBrush = Brushes.Black;


        // 额外的边缘/内边距加粗线（模拟 MATLAB 中 200px 处的加粗线）
        public bool AddEdgeLines { get => _AddEdgeLines; set { _AddEdgeLines = value; OnPropertyChanged(); } }
        private bool _AddEdgeLines = true;

        public int EdgeOffsetPx { get => _EdgeOffsetPx; set { _EdgeOffsetPx = value; OnPropertyChanged(); } }
        private int _EdgeOffsetPx = 200;

        public int EdgeThickness { get => _EdgeThickness; set { _EdgeThickness = value; OnPropertyChanged(); } }
        private int _EdgeThickness = 9;

        // 是否水平拼接两张（等价于 [img, img]）
        public bool DuplicateHorizontally { get => _DuplicateHorizontally; set { _DuplicateHorizontally = value; OnPropertyChanged(); } }
        private bool _DuplicateHorizontally = true;

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

    [DisplayName("十字网格"),Browsable(false)]
    public class PatternCrossGrid : IPatternBase<PatternCrossGridConfig>
    {
        public override UserControl GetPatternEditor() => new CrossGridEditor(Config); // 可自定义编辑器
        public override string GetTemplateName()
        {
            string baseName = "CrossGrid" + "_" + DateTime.Now.ToString("HHmmss");
            
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

            Scalar bg = Config.BackgroundBrush.ToScalar();
            Scalar line = Config.LineBrush.ToScalar();

            Mat grid = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, bg);

            // 计算水平线位置（用 fovHeight）
            if (Config.NumLinesHorizontal > 0 && Config.SpacingHorizontal > 0)
            {
                int startY = (fovHeight - (Config.NumLinesHorizontal * Config.SpacingHorizontal)) + 1; // 对齐 MATLAB 逻辑
                for (int i = 0; i < Config.NumLinesHorizontal; i++)
                {
                    int y = startY + i * Config.SpacingHorizontal;
                    DrawHorizontalLine(grid, y, Config.HorizontalLineWidth, line);
                }
            }

            // 计算竖直线位置（用 fovWidth）
            if (Config.NumLinesVertical > 0 && Config.SpacingVertical > 0)
            {
                int startX = (fovWidth - (Config.NumLinesVertical * Config.SpacingVertical)) + 1; // 对齐 MATLAB 逻辑
                for (int i = 0; i < Config.NumLinesVertical; i++)
                {
                    int x = startX + i * Config.SpacingVertical;
                    DrawVerticalLine(grid, x, Config.VerticalLineWidth, line);
                }
            }

            // 边缘/内边距加粗线，默认在距离边界 EdgeOffsetPx 处加粗（水平+竖直）
            if (Config.AddEdgeLines && Config.EdgeOffsetPx > 0 && Config.EdgeThickness > 0)
            {
                int off = Config.EdgeOffsetPx;
                int t = Config.EdgeThickness;

                // 水平两条
                if (off >= 0 && off < fovHeight)
                    DrawHorizontalLine(grid, off, t, line);
                if (fovHeight - off >= 0 && fovHeight - off < fovHeight)
                    DrawHorizontalLine(grid, fovHeight - off, t, line);

                // 竖直两条
                if (off >= 0 && off < fovWidth)
                    DrawVerticalLine(grid, off, t, line);
                if (fovWidth - off >= 0 && fovWidth - off < fovWidth)
                    DrawVerticalLine(grid, fovWidth - off, t, line);
            }

            if (Config.DuplicateHorizontally)
            {
                Mat combined = new Mat();
                Cv2.HConcat(new[] { grid, grid }, combined);
                
                // If dimensions match the entire image, return directly
                if (fovWidth * 2 == width && fovHeight == height)
                {
                    return combined;
                }
                else
                {
                    // Create background mat and paste combined grid in center
                    var mat = new Mat(height, width, MatType.CV_8UC3, bg);
                    int startX = (width - fovWidth * 2) / 2;
                    int startY = (height - fovHeight) / 2;
                    int combinedWidth = Math.Min(fovWidth * 2, width - startX);

                    combined[new Rect(0, 0, combinedWidth, fovHeight)].CopyTo(mat[new Rect(startX, startY, combinedWidth, fovHeight)]);
                    combined.Dispose();
                    grid.Dispose();

                    return mat;
                }
            }

            // If dimensions match the entire image, return directly
            if (fovWidth == width && fovHeight == height)
            {
                return grid;
            }
            else
            {
                // Create background mat and paste grid in center
                var mat = new Mat(height, width, MatType.CV_8UC3, bg);
                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                grid.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                grid.Dispose();

                return mat;
            }
        }

        private static void DrawHorizontalLine(Mat img, int centerY, int thickness, Scalar color)
        {
            if (thickness <= 0) return;
            int h = img.Rows;
            int w = img.Cols;
            if (h <= 0 || w <= 0) return;

            // 以 centerY 为中心画 thickness 像素高的矩形
            int half = thickness / 2;
            int y0 = centerY - half;
            if (y0 < 0 || y0 >= h) return;

            // 裁剪高度，避免越界
            int y = Math.Max(0, y0);
            int rectH = Math.Min(thickness, h - y);
            if (rectH <= 0) return;

            Cv2.Rectangle(img, new Rect(0, y, w, rectH), color, -1);
        }

        private static void DrawVerticalLine(Mat img, int centerX, int thickness, Scalar color)
        {
            if (thickness <= 0) return;
            int h = img.Rows;
            int w = img.Cols;
            if (h <= 0 || w <= 0) return;

            int half = thickness / 2;
            int x0 = centerX - half;
            if (x0 < 0 || x0 >= w) return;

            int x = Math.Max(0, x0);
            int rectW = Math.Min(thickness, w - x);
            if (rectW <= 0) return;

            Cv2.Rectangle(img, new Rect(x, 0, rectW, h), color, -1);
        }
    }
}
