using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Ring
{
    public enum SolidSizeMode
    {
        ByFieldOfView,
        ByPixelSize
    }

    public class PatternRingConfig:ViewModelBase,IConfig
    {

        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;

        public int RingWidth { get => _RingWidth; set { _RingWidth = value; OnPropertyChanged(); } }
        private int _RingWidth = 30;

        public List<int> RingOffsets { get => _RingOffsets; set { _RingOffsets = value; OnPropertyChanged(); } }
        private List<int> _RingOffsets = new List<int> { 0, 100, 200, 400 };

        [DisplayName("是否绘制中心线")]
        public bool DrawCenterLine { get => _DrawCenterLine; set { _DrawCenterLine = value; OnPropertyChanged(); } }
        private bool _DrawCenterLine = true;

        [DisplayName("尺寸模式")]
        public SolidSizeMode SizeMode { get => _SizeMode; set { _SizeMode = value; OnPropertyChanged(); } }
        private SolidSizeMode _SizeMode = SolidSizeMode.ByFieldOfView;

        [PropertyVisibility(nameof(SizeMode), SolidSizeMode.ByFieldOfView)]
        [DisplayName("视场系数X")]
        public double FieldOfViewX { get => _FieldOfViewX; set { _FieldOfViewX = value; OnPropertyChanged(); } }
        private double _FieldOfViewX = 1.0;

        [PropertyVisibility(nameof(SizeMode), SolidSizeMode.ByFieldOfView)]
        [DisplayName("视场系数Y")]
        public double FieldOfViewY { get => _FieldOfViewY; set { _FieldOfViewY = value; OnPropertyChanged(); } }
        private double _FieldOfViewY = 1.0;

        [PropertyVisibility(nameof(SizeMode), SolidSizeMode.ByPixelSize)]
        [DisplayName("像素宽度")]
        public int PixelWidth { get => _PixelWidth; set { _PixelWidth = value; OnPropertyChanged(); } }
        private int _PixelWidth = 100;

        [PropertyVisibility(nameof(SizeMode), SolidSizeMode.ByPixelSize)]
        [DisplayName("像素高度")]
        public int PixelHeight { get => _PixelHeight; set { _PixelHeight = value; OnPropertyChanged(); } }
        private int _PixelHeight = 100;
    }

    [DisplayName("Ring")]
    public class PatternRing : IPatternBase<PatternRingConfig>
    {
        public override UserControl GetPatternEditor() => new RingEditor(Config);
        public override string GetTemplateName()
        {
            string baseName = "Ring_" + DateTime.Now.ToString("HHmmss");
            
            // Add FOV/Pixel suffix
            if (Config.SizeMode == SolidSizeMode.ByPixelSize)
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
            if (Config.SizeMode == SolidSizeMode.ByPixelSize)
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

            int ringWidth = Config.RingWidth;

            // Generate ring pattern within FOV dimensions
            var ring = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            double centerX = fovWidth / 2.0;
            double centerY = fovHeight / 2.0;
            double maxR = Math.Min(centerX, centerY) - ringWidth / 2.0;

            foreach (var offset in Config.RingOffsets)
            {
                double outerRadius = maxR - offset;
                double innerRadius = outerRadius - ringWidth;
                Cv2.Circle(ring, new Point(centerX, centerY), (int)Math.Round((outerRadius + innerRadius) / 2), Config.AltBrush.ToScalar(), ringWidth, LineTypes.AntiAlias);
            }

            // 中心线
            if (Config.DrawCenterLine)
            {
                // 水平
                Cv2.Line(ring, new Point(0, fovHeight / 2), new Point(fovWidth - 1, fovHeight / 2), Config.AltBrush.ToScalar(), 2);
                // 垂直
                Cv2.Line(ring, new Point(fovWidth / 2, 0), new Point(fovWidth / 2, fovHeight - 1), Config.AltBrush.ToScalar(), 2);
            }

            // If dimensions match the entire image, return directly
            if (fovWidth == width && fovHeight == height)
            {
                return ring;
            }
            else
            {
                // Create background mat and paste ring in center
                var mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                ring.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                ring.Dispose();

                return mat;
            }
        }
    }
}
