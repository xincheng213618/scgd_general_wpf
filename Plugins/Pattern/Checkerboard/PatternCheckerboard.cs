using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Checkerboard
{
    public enum CheckerboardSizeMode
    {
        ByGridCount,
        ByCellSize
    }


    public class PatternCheckerboardConfig:ViewModelBase,IConfig
    {


        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.White;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.Black;

        [PropertyVisibility(nameof(CheckerboardSizeMode), CheckerboardSizeMode.ByGridCount)]
        public int GridX { get => _GridX; set { _GridX = value; OnPropertyChanged(); } }
        private int _GridX = 8;
        [PropertyVisibility(nameof(CheckerboardSizeMode), CheckerboardSizeMode.ByGridCount)]
        public int GridY { get => _GridY; set { _GridY = value; OnPropertyChanged(); } }
        private int _GridY = 8;

        [PropertyVisibility(nameof(CheckerboardSizeMode), CheckerboardSizeMode.ByCellSize)]
        public int CellW { get => _CellW; set { _CellW = value; OnPropertyChanged(); } }
        private int _CellW = 32;

        [PropertyVisibility(nameof(CheckerboardSizeMode), CheckerboardSizeMode.ByCellSize)]
        public int CellH { get => _CellH; set { _CellH = value; OnPropertyChanged(); } }
        private int _CellH = 32;

        [DisplayName("绘制模式")]
        public CheckerboardSizeMode CheckerboardSizeMode { get => _CheckerboardSizeMode; set { _CheckerboardSizeMode = value; OnPropertyChanged(); } }
        private CheckerboardSizeMode _CheckerboardSizeMode = CheckerboardSizeMode.ByGridCount;

        public string MainBrushTag { get => _MainBrushTag; set { _MainBrushTag = value; OnPropertyChanged(); } }
        private string _MainBrushTag = "W";
        
        public string AltBrushTag { get => _AltBrushTag; set { _AltBrushTag = value; OnPropertyChanged(); } }
        private string _AltBrushTag = "K";

        [DisplayName("视场背景")]
        public SolidColorBrush BackGroundBrush { get => _BackGroundBrush; set { _BackGroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackGroundBrush = Brushes.Black;

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

    [DisplayName("棋盘格")]
    public class PatternCheckerboard : IPatternBase<PatternCheckerboardConfig>
    {
        public override UserControl GetPatternEditor() => new CheckerboardEditor(Config);
        public override string GetTemplateName()
        {
            string str = string.Empty;
            if (Config.CheckerboardSizeMode == CheckerboardSizeMode.ByGridCount)
            {
                str = $"{Config.GridX}x{Config.GridY}";
            }
            else 
            {
                str = $"{Config.CellW}x{Config.CellH}";
            }
            string baseName = "Checkerboard" + "_" + Config.MainBrushTag + Config.AltBrushTag + "_" + str;
            
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

            // Generate checkerboard pattern
            Mat checker = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            double gridX, gridY, cellW, cellH;
            if (Config.CheckerboardSizeMode == CheckerboardSizeMode.ByGridCount)
            {
                gridX = Config.GridX;
                gridY = Config.GridY;
                cellW = (double)fovWidth / gridX;
                cellH = (double)fovHeight / gridY;
            }
            else // ByCellSize
            {
                cellW = Config.CellW;
                cellH = Config.CellH;
                gridX = fovWidth / cellW;
                gridY = fovHeight / cellH;
                // 保证至少有一格
                if (gridX < 1) gridX = 1;
                if (gridY < 1) gridY = 1;
            }

            for (int y = 0; y < gridY; y++)
            {
                for (int x = 0; x < gridX; x++)
                {
                    double cellStartX = x * cellW;
                    double cellStartY = y * cellH;
                    double w = (x == gridX - 1) ? fovWidth - cellStartX : cellW;
                    double h = (y == gridY - 1) ? fovHeight - cellStartY : cellH;
                    if ((x + y) % 2 == 1)
                        Cv2.Rectangle(checker, new Rect((int)cellStartX, (int)cellStartY, (int)Math.Ceiling(w), (int)Math.Ceiling(h)), Config.AltBrush.ToScalar(), -1);
                }
            }

            // If dimensions match the entire image, return directly
            if (fovWidth == width && fovHeight == height)
            {
                return checker;
            }
            else
            {
                // Create background mat and paste checkerboard in center
                var mat = new Mat(height, width, MatType.CV_8UC3, Config.BackGroundBrush.ToScalar());
                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                checker.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                checker.Dispose();
                return mat;
            }
        }
    }
}
