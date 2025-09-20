using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using static SkiaSharp.HarfBuzz.SKShaper;

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


        // 新增：视场坐标 Json 属性（支持界面配置）
        public string FieldXJson
        {
            get => JsonConvert.SerializeObject(FieldX);
            set
            {
                try
                {
                    FieldX = JsonConvert.DeserializeObject<List<double>>(value) ?? new List<double>();
                }
                catch
                {
                    FieldX = new List<double>();
                }
                OnPropertyChanged(nameof(FieldXJson));
                OnPropertyChanged(nameof(FieldX));
            }
        }

        public string FieldYJson
        {
            get => JsonConvert.SerializeObject(FieldY);
            set
            {
                try
                {
                    FieldY = JsonConvert.DeserializeObject<List<double>>(value) ?? new List<double>();
                }
                catch
                {
                    FieldY = new List<double>();
                }
                OnPropertyChanged(nameof(FieldYJson));
                OnPropertyChanged(nameof(FieldY));
            }
        }

        [JsonIgnore]
        public List<double> FieldX
        {
            get => _FieldX;
            set
            {
                _FieldX = value;
                OnPropertyChanged(nameof(FieldX));
                OnPropertyChanged(nameof(FieldXJson));
            }
        }
        private List<double> _FieldX = new List<double>();

        [JsonIgnore]
        public List<double> FieldY
        {
            get => _FieldY;
            set
            {
                _FieldY = value;
                OnPropertyChanged(nameof(FieldY));
                OnPropertyChanged(nameof(FieldYJson));
            }
        }
        private List<double> _FieldY = new List<double>();
    }

    [DisplayName("十字")]
    public class PatternCross : IPatternBase<PatternCrossConfig>
    {
        public override UserControl GetPatternEditor() => new CrossEditor(Config);
        public override string GetTemplateName()
        {
            return "Cross" + "_" + Config.MainBrushTag + Config.AltBrushTag + $"_{Config.HorizontalWidth}x{Config.VerticalWidth}";
        }
        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            if (Config.FieldX.Count == 0 || Config.FieldY.Count == 0)
            {
                // 居中全宽全高十字
                DrawCross(mat, width / 2, height / 2, Config.HorizontalWidth, width, Config.VerticalWidth, height, Config.AltBrush.ToScalar());
            }
            else
            {
                int coordCount = Math.Min(Config.FieldX.Count, Config.FieldY.Count);
                int cent_x = width / 2;
                int cent_y = height / 2;
                int w = 10;
                for (int j = 0; j < coordCount; j++)
                {
                    double fx = Config.FieldX[j];
                    double fy = Config.FieldY[j];

                    // 左上
                    DrawCross(mat, (int)(cent_x - cent_x * fx), (int)(cent_y - cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                    DrawCross(mat, (int)(cent_x + cent_x * fx), (int)(cent_y - cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                    DrawCross(mat, (int)(cent_x - cent_x * fx), (int)(cent_y + cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                    DrawCross(mat, (int)(cent_x + cent_x * fx), (int)(cent_y + cent_y * fy), Config.HorizontalWidth, Config.HorizontalLength, Config.VerticalWidth, Config.VerticalLength, Config.AltBrush.ToScalar());
                }
            }
            return mat;
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
