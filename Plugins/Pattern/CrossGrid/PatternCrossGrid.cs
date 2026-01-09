using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.CrossGrid
{
    /// <summary>
    /// 十字网格绘制模式
    /// </summary>
    public enum CrossGridDrawMode
    {
        /// <summary>
        /// 按数量绘制（指定线条数量，自动计算间隔）
        /// </summary>
        [Description("按数量")]
        ByCount,
        /// <summary>
        /// 按间隔绘制（指定间隔，自动计算线条数量）
        /// </summary>
        [Description("按间隔")]
        BySpacing
    }

    public class PatternCrossGridConfig:ViewModelBase,IConfig
    {
        [DisplayName("绘制模式")]
        public CrossGridDrawMode DrawMode { get => _DrawMode; set { _DrawMode = value; OnPropertyChanged(); } }
        private CrossGridDrawMode _DrawMode = CrossGridDrawMode.ByCount;

        // 按数量模式的参数
        [PropertyVisibility(nameof(DrawMode), CrossGridDrawMode.ByCount)]
        [DisplayName("水平线条数")]
        public int NumLinesHorizontal { get => _NumLinesHorizontal; set { _NumLinesHorizontal = value; OnPropertyChanged(); } }
        private int _NumLinesHorizontal = 5;

        [PropertyVisibility(nameof(DrawMode), CrossGridDrawMode.ByCount)]
        [DisplayName("垂直线条数")]
        public int NumLinesVertical { get => _NumLinesVertical; set { _NumLinesVertical = value; OnPropertyChanged(); } }
        private int _NumLinesVertical = 5;

        // 按间隔模式的参数
        [PropertyVisibility(nameof(DrawMode), CrossGridDrawMode.BySpacing)]
        [DisplayName("水平间隔")]
        public int SpacingHorizontal { get => _SpacingHorizontal; set { _SpacingHorizontal = value; OnPropertyChanged(); } }
        private int _SpacingHorizontal = 200;

        [PropertyVisibility(nameof(DrawMode), CrossGridDrawMode.BySpacing)]
        [DisplayName("垂直间隔")]
        public int SpacingVertical { get => _SpacingVertical; set { _SpacingVertical = value; OnPropertyChanged(); } }
        private int _SpacingVertical = 200;

        [DisplayName("水平线宽度")]
        public int HorizontalLineWidth { get => _HorizontalLineWidth; set { _HorizontalLineWidth = value; OnPropertyChanged(); } }
        private int _HorizontalLineWidth = 5;

        [DisplayName("垂直线宽度")]
        public int VerticalLineWidth { get => _VerticalLineWidth; set { _VerticalLineWidth = value; OnPropertyChanged(); } }
        private int _VerticalLineWidth = 5;


        public SolidColorBrush LineBrush { get => _LineBrush; set { _LineBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _LineBrush = Brushes.White;

        public SolidColorBrush BackgroundBrush { get => _BackgroundBrush; set { _BackgroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackgroundBrush = Brushes.Black;


        // 边缘线配置
        [DisplayName("绘制边缘线")]
        public bool AddEdgeLines { get => _AddEdgeLines; set { _AddEdgeLines = value; OnPropertyChanged(); } }
        private bool _AddEdgeLines;

        [PropertyVisibility(nameof(AddEdgeLines))]
        [DisplayName("边缘偏移量")]
        public int EdgeOffsetPx { get => _EdgeOffsetPx; set { _EdgeOffsetPx = value; OnPropertyChanged(); } }
        private int _EdgeOffsetPx = 200;

        [PropertyVisibility(nameof(AddEdgeLines))]
        [DisplayName("边缘线宽度")]
        public int EdgeThickness { get => _EdgeThickness; set { _EdgeThickness = value; OnPropertyChanged(); } }
        private int _EdgeThickness = 5;

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

    [DisplayName("十字网格")]
    public class PatternCrossGrid : IPatternBase<PatternCrossGridConfig>
    {
        public override UserControl GetPatternEditor() => new CrossGridEditor(Config); // 可自定义编辑器
        public override string GetTemplateName()
        {
            string baseName = "CrossGrid";
            if (Config.DrawMode == CrossGridDrawMode.ByCount)
            {
                baseName += $"_ByCount_{Config.NumLinesHorizontal}x{Config.NumLinesVertical}";
            }
            else
            {
                baseName += $"_BySpacing_{Config.SpacingHorizontal}x{Config.SpacingVertical}";
            }

            if (Config.AddEdgeLines)
            {
                baseName += $"_EdgeLines_{Config.EdgeOffsetPx}x{Config.EdgeThickness}";
            }
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
                    baseName += $"_View_{Config.FieldOfViewX:0.##}x{Config.FieldOfViewY:0.##}";
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

            // 根据绘制模式计算线条位置
            if (Config.DrawMode == CrossGridDrawMode.ByCount)
            {
                // 按数量模式：指定线条数量，均匀分布
                DrawLinesByCount(grid, Config.NumLinesHorizontal, Config.NumLinesVertical, 
                    Config.HorizontalLineWidth, Config.VerticalLineWidth, line);
            }
            else // BySpacing
            {
                // 按间隔模式：指定间隔，自动计算线条数量
                DrawLinesBySpacing(grid, Config.SpacingHorizontal, Config.SpacingVertical,
                    Config.HorizontalLineWidth, Config.VerticalLineWidth, line);
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

        /// <summary>
        /// 按数量模式绘制：指定线条数量，均匀分布
        /// </summary>
        private static void DrawLinesByCount(Mat img, int numHorizontal, int numVertical, 
            int hThickness, int vThickness, Scalar color)
        {
            int h = img.Rows;
            int w = img.Cols;

            // 绘制水平线：在高度方向均匀分布
            if (numHorizontal > 0)
            {
                for (int i = 0; i < numHorizontal; i++)
                {
                    // 均匀分布：将高度分成 (numHorizontal + 1) 段，线条在每段之间
                    int y = (int)((i + 1) * h / (double)(numHorizontal + 1));
                    DrawHorizontalLine(img, y, hThickness, color);
                }
            }

            // 绘制垂直线：在宽度方向均匀分布
            if (numVertical > 0)
            {
                for (int i = 0; i < numVertical; i++)
                {
                    // 均匀分布：将宽度分成 (numVertical + 1) 段，线条在每段之间
                    int x = (int)((i + 1) * w / (double)(numVertical + 1));
                    DrawVerticalLine(img, x, vThickness, color);
                }
            }
        }

        /// <summary>
        /// 按间隔模式绘制：指定间隔，自动计算线条数量并居中
        /// </summary>
        private static void DrawLinesBySpacing(Mat img, int hSpacing, int vSpacing,
            int hThickness, int vThickness, Scalar color)
        {
            int h = img.Rows;
            int w = img.Cols;

            // 绘制水平线：按指定间隔，从中心向两边分布
            if (hSpacing > 0)
            {
                int centerY = h / 2;
                int numLines = (h / hSpacing) + 1;  // 计算能容纳的线条数
                
                // 从中心线开始
                DrawHorizontalLine(img, centerY, hThickness, color);
                
                // 向上和向下绘制
                for (int i = 1; i * hSpacing < h / 2; i++)
                {
                    int yUp = centerY - i * hSpacing;
                    int yDown = centerY + i * hSpacing;
                    
                    if (yUp >= 0 && yUp < h)
                        DrawHorizontalLine(img, yUp, hThickness, color);
                    if (yDown >= 0 && yDown < h)
                        DrawHorizontalLine(img, yDown, hThickness, color);
                }
            }

            // 绘制垂直线：按指定间隔，从中心向两边分布
            if (vSpacing > 0)
            {
                int centerX = w / 2;
                int numLines = (w / vSpacing) + 1;  // 计算能容纳的线条数
                
                // 从中心线开始
                DrawVerticalLine(img, centerX, vThickness, color);
                
                // 向左和向右绘制
                for (int i = 1; i * vSpacing < w / 2; i++)
                {
                    int xLeft = centerX - i * vSpacing;
                    int xRight = centerX + i * vSpacing;
                    
                    if (xLeft >= 0 && xLeft < w)
                        DrawVerticalLine(img, xLeft, vThickness, color);
                    if (xRight >= 0 && xRight < w)
                        DrawVerticalLine(img, xRight, vThickness, color);
                }
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
