using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.LinePairMTF
{
    public class PatternLinePairMTFConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush LineBrush { get => _LineBrush; set { _LineBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _LineBrush = Brushes.Black;

        public SolidColorBrush BackgroundBrush { get =>_BackgroundBrush; set {_BackgroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackgroundBrush = Brushes.Green;


        public int LineThickness { get => _LineThickness; set { _LineThickness = value; OnPropertyChanged(); } }
        private int _LineThickness = 2;

        public int LineLength { get => _LineLength; set { _LineLength = value; OnPropertyChanged(); } }
        private int _LineLength = 40;

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
                OnPropertyChanged();
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
                OnPropertyChanged();
                OnPropertyChanged(nameof(FieldY));
            }
        }
        [JsonIgnore]
        public List<double> FieldX { get; set; } = new List<double> { 0,  0.5, 0.8};
        [JsonIgnore]
        public List<double> FieldY { get; set; } = new List<double> { 0,0.5, 0.8 };

        public string LineBrushTag { get; set; } = "K";
        public string BackgroundBrushTag { get; set; } = "W";
    }

    [DisplayName("MTF")]
    public class PatternLinePairMTF : IPatternBase<PatternLinePairMTFConfig>
    {
        public override UserControl GetPatternEditor() => new LinePairMTFEditor(Config);

        public override string GetTemplateName()
        {
            return "FourLinePairMTF" + "_" + Config.LineBrushTag + Config.BackgroundBrushTag +
                $"_{Config.LineThickness}_{Config.LineLength}";
        }

        public override Mat Gen(int height, int width)
        {
            // 背景色
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.BackgroundBrush.ToScalar());
            int cent_x = width / 2;
            int cent_y = height / 2;

            int count = Math.Min(Config.FieldX.Count, Config.FieldY.Count);

            for (int i = 0; i < count; i++)
            {
                double fx = Config.FieldX[i];
                double fy = Config.FieldY[i];

                int x1 = (int)(cent_x - cent_x * fx);
                int y1 = (int)(cent_y - cent_y * fy);
                DrawFourLinePair(mat, width, height, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x1, y1);

                int x2 = (int)(cent_x + cent_x * fx);
                int y2 = (int)(cent_y - cent_y * fy);
                DrawFourLinePair(mat, width, height, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x2, y2);

                int x3 = (int)(cent_x - cent_x * fx);
                int y3 = (int)(cent_y + cent_y * fy);
                DrawFourLinePair(mat, width, height, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x3, y3);

                int x4 = (int)(cent_x + cent_x * fx);
                int y4 = (int)(cent_y + cent_y * fy);
                DrawFourLinePair(mat, width, height, Config.LineLength, Config.LineThickness, Config.LineBrush.ToScalar(), x4, y4);
            }
            return mat;
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
