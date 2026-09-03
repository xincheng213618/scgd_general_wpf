using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.QuadrantGrating
{
    public enum GratingLayoutMode
    {
        ByGridCount,
        ByCellSize
    }

    public class PatternQuadrantGratingConfig : ViewModelBase, IConfig
    {
        [DisplayName("线条颜色"), PropertyEditorType(typeof(PatternBrushPropertiesEditor))]
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); OnPropertyChanged(nameof(MainBrushTag)); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        [Browsable(false)]
        public string MainBrushTag => MainBrush.ToColorTag();

        [DisplayName("间隔颜色"), PropertyEditorType(typeof(PatternBrushPropertiesEditor))]
        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); OnPropertyChanged(nameof(AltBrushTag)); } }
        private SolidColorBrush _AltBrush = Brushes.White;

        [Browsable(false)]
        public string AltBrushTag => AltBrush.ToColorTag();

        [DisplayName("线宽（像素）")]
        public int LineWidth { get => _LineWidth; set { _LineWidth = value; OnPropertyChanged(); } }
        private int _LineWidth = 2;

        [DisplayName("排列模式")]
        public GratingLayoutMode LayoutMode { get => _LayoutMode; set { _LayoutMode = value; OnPropertyChanged(); } }
        private GratingLayoutMode _LayoutMode = GratingLayoutMode.ByGridCount;

        [PropertyVisibility(nameof(LayoutMode), GratingLayoutMode.ByGridCount)]
        [DisplayName("列数")]
        public int Columns { get => _Columns; set { _Columns = value; OnPropertyChanged(); } }
        private int _Columns = 2;

        [PropertyVisibility(nameof(LayoutMode), GratingLayoutMode.ByGridCount)]
        [DisplayName("行数")]
        public int Rows { get => _Rows; set { _Rows = value; OnPropertyChanged(); } }
        private int _Rows = 2;

        [PropertyVisibility(nameof(LayoutMode), GratingLayoutMode.ByCellSize)]
        [DisplayName("单元格宽度（像素）")]
        public int CellWidth { get => _CellWidth; set { _CellWidth = value; OnPropertyChanged(); } }
        private int _CellWidth = 320;

        [PropertyVisibility(nameof(LayoutMode), GratingLayoutMode.ByCellSize)]
        [DisplayName("单元格高度（像素）")]
        public int CellHeight { get => _CellHeight; set { _CellHeight = value; OnPropertyChanged(); } }
        private int _CellHeight = 240;

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
        private int _PixelWidth = 640;

        [PropertyVisibility(nameof(SizeMode), PatternSizeMode.ByPixelSize)]
        [DisplayName("像素高度")]
        public int PixelHeight { get => _PixelHeight; set { _PixelHeight = value; OnPropertyChanged(); } }
        private int _PixelHeight = 480;

        [DisplayName("视场背景"), PropertyEditorType(typeof(PatternBrushPropertiesEditor))]
        public SolidColorBrush BackGroundBrush { get => _BackGroundBrush; set { _BackGroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackGroundBrush = Brushes.Black;
    }

    [DisplayName("四象限线栅")]
    [Description("按行列数量或单元格像素尺寸交替绘制水平和垂直等宽线栅")]
    public class PatternQuadrantGrating : IPatternBase<PatternQuadrantGratingConfig>
    {
        public override UserControl GetPatternEditor() => new QuadrantGratingEditor(Config);

        public override string GetTemplateName()
        {
            string layout = Config.LayoutMode == GratingLayoutMode.ByGridCount
                ? $"Count_{Math.Max(1, Config.Columns)}x{Math.Max(1, Config.Rows)}"
                : $"Cell_{Math.Max(1, Config.CellWidth)}x{Math.Max(1, Config.CellHeight)}";
            string baseName = $"QuadrantGrating_{Config.MainBrushTag}{Config.AltBrushTag}_L{Math.Max(1, Config.LineWidth)}_{layout}";
            if (Config.SizeMode == PatternSizeMode.ByPixelSize)
            {
                baseName += $"_Pixel_{Config.PixelWidth}x{Config.PixelHeight}";
            }
            else if (Config.FieldOfViewX != 1.0 || Config.FieldOfViewY != 1.0)
            {
                baseName += $"_FOV_{Config.FieldOfViewX:0.##}x{Config.FieldOfViewY:0.##}";
            }
            return baseName;
        }

        public override Mat Gen(int height, int width)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

            int fovWidth;
            int fovHeight;
            if (Config.SizeMode == PatternSizeMode.ByPixelSize)
            {
                fovWidth = Math.Clamp(Config.PixelWidth, 1, width);
                fovHeight = Math.Clamp(Config.PixelHeight, 1, height);
            }
            else
            {
                double fovX = Math.Clamp(Config.FieldOfViewX, 0, 1.0);
                double fovY = Math.Clamp(Config.FieldOfViewY, 0, 1.0);
                fovWidth = Math.Clamp((int)(width * fovX), 1, width);
                fovHeight = Math.Clamp((int)(height * fovY), 1, height);
            }

            var grating = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.AltBrush.ToScalar());
            int lineWidth = Math.Clamp(Config.LineWidth, 1, Math.Max(fovWidth, fovHeight));
            Scalar lineColor = Config.MainBrush.ToScalar();

            int columns;
            int rows;
            int cellWidth = 0;
            int cellHeight = 0;
            if (Config.LayoutMode == GratingLayoutMode.ByGridCount)
            {
                columns = Math.Clamp(Config.Columns, 1, fovWidth);
                rows = Math.Clamp(Config.Rows, 1, fovHeight);
            }
            else
            {
                cellWidth = Math.Clamp(Config.CellWidth, 1, fovWidth);
                cellHeight = Math.Clamp(Config.CellHeight, 1, fovHeight);
                columns = (fovWidth - 1) / cellWidth + 1;
                rows = (fovHeight - 1) / cellHeight + 1;
            }

            for (int row = 0; row < rows; row++)
            {
                int top = Config.LayoutMode == GratingLayoutMode.ByGridCount ? (int)((long)row * fovHeight / rows) : row * cellHeight;
                int bottom = Config.LayoutMode == GratingLayoutMode.ByGridCount ? (int)((long)(row + 1) * fovHeight / rows) : Math.Min(top + cellHeight, fovHeight);
                for (int column = 0; column < columns; column++)
                {
                    int left = Config.LayoutMode == GratingLayoutMode.ByGridCount ? (int)((long)column * fovWidth / columns) : column * cellWidth;
                    int right = Config.LayoutMode == GratingLayoutMode.ByGridCount ? (int)((long)(column + 1) * fovWidth / columns) : Math.Min(left + cellWidth, fovWidth);
                    var cell = new Rect(left, top, right - left, bottom - top);
                    if ((row + column) % 2 == 0)
                    {
                        DrawHorizontalBands(grating, cell, lineWidth, lineColor);
                    }
                    else
                    {
                        DrawVerticalBands(grating, cell, lineWidth, lineColor);
                    }
                }
            }

            if (fovWidth == width && fovHeight == height)
            {
                return grating;
            }

            var result = new Mat(height, width, MatType.CV_8UC3, Config.BackGroundBrush.ToScalar());
            int startX = (width - fovWidth) / 2;
            int startY = (height - fovHeight) / 2;
            grating.CopyTo(result[new Rect(startX, startY, fovWidth, fovHeight)]);
            grating.Dispose();
            return result;
        }

        private static void DrawHorizontalBands(Mat image, Rect area, int lineWidth, Scalar color)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            int period = lineWidth * 2;
            int bottom = area.Y + area.Height;
            for (int y = area.Y + lineWidth; y < bottom; y += period)
            {
                Cv2.Rectangle(image, new Rect(area.X, y, area.Width, Math.Min(lineWidth, bottom - y)), color, -1);
            }
        }

        private static void DrawVerticalBands(Mat image, Rect area, int lineWidth, Scalar color)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            int period = lineWidth * 2;
            int right = area.X + area.Width;
            for (int x = area.X + lineWidth; x < right; x += period)
            {
                Cv2.Rectangle(image, new Rect(x, area.Y, Math.Min(lineWidth, right - x), area.Height), color, -1);
            }
        }
    }
}
