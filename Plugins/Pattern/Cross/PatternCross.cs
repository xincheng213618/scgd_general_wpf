using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Cross
{

    public class PatternCrossConfig : ViewModelBase, IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;


        public int HorizontalWidth { get => _HorizontalWidth; set { _HorizontalWidth = value; OnPropertyChanged(); } }
        private int _HorizontalWidth = 1;

        public int VerticalWidth { get => _VerticalWidth; set { _VerticalWidth = value; OnPropertyChanged(); } }
        private int _VerticalWidth = 1;

        public string MainBrushTag { get; set; } = "K";
        public string AltBrushTag { get; set; } = "W";

        public int HorizontalLength { get => _HorizontalLength; set { _HorizontalLength = value; OnPropertyChanged(); } }
        private int _HorizontalLength = 10;

        public int VerticalLength { get => _VerticalLength; set { _VerticalLength = value; OnPropertyChanged(); } }
        private int _VerticalLength = 10;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<double> FieldX
        {
            get => _FieldX;
            set
            {
                _FieldX = value;
                OnPropertyChanged(nameof(FieldX));
            }
        }
        private List<double> _FieldX = new List<double>();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<double> FieldY
        {
            get => _FieldY;
            set
            {
                _FieldY = value;
                OnPropertyChanged(nameof(FieldY));
            }
        }
        private List<double> _FieldY = new List<double>();

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

    [DisplayName("十字")]
    public class PatternCross : IPatternBase<PatternCrossConfig>
    {
        public override UserControl GetPatternEditor() => new CrossEditor(Config);
        public override string GetTemplateName()
        {
            string baseName = "Cross" + "_" + Config.MainBrushTag + Config.AltBrushTag + $"_{Config.HorizontalWidth}x{Config.VerticalWidth}_{Config.HorizontalLength}x{Config.VerticalLength}";
            
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

            // Generate cross pattern within FOV dimensions
            Mat cross = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            if (Config.FieldX.Count == 0 || Config.FieldY.Count == 0)
            {
                // 居中全宽全高十字
                DrawCross(cross, fovWidth / 2, fovHeight / 2, Config.HorizontalWidth, fovWidth, Config.VerticalWidth, fovHeight, Config.AltBrush.ToScalar());
            }
            else
            {
                int coordCount = Math.Min(Config.FieldX.Count, Config.FieldY.Count);
                int cent_x = fovWidth / 2;
                int cent_y = fovHeight / 2;
                for (int j = 0; j < coordCount; j++)
                {
                    double fx = Config.FieldX[j];
                    double fy = Config.FieldY[j];

                    // 左上右上左下右下
                    DrawCross(cross, (int)(cent_x - cent_x * fx), (int)(cent_y - cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                    DrawCross(cross, (int)(cent_x + cent_x * fx), (int)(cent_y - cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                    DrawCross(cross, (int)(cent_x - cent_x * fx), (int)(cent_y + cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                    DrawCross(cross, (int)(cent_x + cent_x * fx), (int)(cent_y + cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                }
            }

            // If dimensions match the entire image, return directly
            if (fovWidth == width && fovHeight == height)
            {
                return cross;
            }
            else
            {
                // Create background mat and paste cross in center
                var mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                cross.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                cross.Dispose();

                return mat;
            }
        }

        private static void DrawCross(Mat mat, int xCenter, int yCenter, int horizontalThickness, int horizontalLength, int verticalThickness, int verticalLength, Scalar altColor)
        {
            // 横线：y 范围在(yCenter - horizontalThickness/2, yCenter + horizontalThickness/2)
            int hStart = Math.Max(0, yCenter - horizontalThickness / 2);
            int hEnd = Math.Min(mat.Height, yCenter + (horizontalThickness + 1) / 2);
            int hlStart = Math.Max(0, xCenter - horizontalLength / 2);
            int hlEnd = Math.Min(mat.Width, xCenter + (horizontalLength + 1) / 2);

            for (int y = hStart; y < hEnd; y++)
            {
                // 横线只画 length 范围
                mat.Row(y).ColRange(hlStart, hlEnd).SetTo(altColor);
            }

            // 竖线：x 范围在(xCenter - verticalThickness/2, xCenter + verticalThickness/2)
            int vStart = Math.Max(0, xCenter - verticalThickness / 2);
            int vEnd = Math.Min(mat.Width, xCenter + (verticalThickness + 1) / 2);
            int vlStart = Math.Max(0, yCenter - verticalLength / 2);
            int vlEnd = Math.Min(mat.Height, yCenter + (verticalLength + 1) / 2);

            for (int x = vStart; x < vEnd; x++)
            {
                // 竖线只画 length 范围
                mat.Col(x).RowRange(vlStart, vlEnd).SetTo(altColor);
            }
        }
    }
}
