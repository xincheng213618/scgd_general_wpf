using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Ring
{
    /// <summary>
    /// 圆环半径模式：按像素半径或按比例系数
    /// </summary>
    public enum RingRadiusMode
    {
        /// <summary>
        /// 按像素半径
        /// </summary>
        ByPixelRadius,
        /// <summary>
        /// 按比例系数（0-1.0）
        /// </summary>
        ByRadiusRatio
    }

    public class PatternRingConfig:ViewModelBase,IConfig
    {

        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;

        public int RingWidth { get => _RingWidth; set { _RingWidth = value; OnPropertyChanged(); } }
        private int _RingWidth = 30;

        [DisplayName("半径模式")]
        public RingRadiusMode RadiusMode { get => _RadiusMode; set { _RadiusMode = value; OnPropertyChanged(); } }
        private RingRadiusMode _RadiusMode = RingRadiusMode.ByRadiusRatio;

        [PropertyVisibility(nameof(RadiusMode), RingRadiusMode.ByPixelRadius)]
        [DisplayName("像素半径列表")]
        public List<int> PixelRadii { get => _PixelRadii; set { _PixelRadii = value; OnPropertyChanged(); } }
        private List<int> _PixelRadii = new List<int> { 50, 100, 150, 200 };

        [PropertyVisibility(nameof(RadiusMode), RingRadiusMode.ByRadiusRatio)]
        [DisplayName("比例系数列表")]
        public List<double> RadiusRatios { get => _RadiusRatios; set { _RadiusRatios = value; OnPropertyChanged(); } }
        private List<double> _RadiusRatios = new List<double> { 0.2, 0.4, 0.6, 0.8 };

        [DisplayName("自定义圆心")]
        public bool UseCustomCenter { get => _UseCustomCenter; set { _UseCustomCenter = value; OnPropertyChanged(); } }
        private bool _UseCustomCenter;

        [PropertyVisibility(nameof(UseCustomCenter), true)]
        [DisplayName("圆心X坐标")]
        public int CenterX { get => _CenterX; set { _CenterX = value; OnPropertyChanged(); } }
        private int _CenterX;

        [PropertyVisibility(nameof(UseCustomCenter), true)]
        [DisplayName("圆心Y坐标")]
        public int CenterY { get => _CenterY; set { _CenterY = value; OnPropertyChanged(); } }
        private int _CenterY;

        [DisplayName("是否绘制中心线")]
        public bool DrawCenterLine { get => _DrawCenterLine; set { _DrawCenterLine = value; OnPropertyChanged(); } }
        private bool _DrawCenterLine = true;

        [PropertyVisibility(nameof(DrawCenterLine), true)]
        [DisplayName("中心线宽度")]
        public int CenterLineWidth { get => _CenterLineWidth; set { _CenterLineWidth = value; OnPropertyChanged(); } }
        private int _CenterLineWidth = 2;

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

    [DisplayName("Ring")]
    public class PatternRing : IPatternBase<PatternRingConfig>
    {
        public override UserControl GetPatternEditor() => new RingEditor(Config);
        public override string GetTemplateName()
        {
            string baseName = "Ring_" + DateTime.Now.ToString("HHmmss");
            
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

            int ringWidth = Config.RingWidth;

            // Generate ring pattern within FOV dimensions
            var ring = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            
            // Calculate center point with validation
            double centerX = Config.UseCustomCenter ? Math.Max(0, Math.Min(Config.CenterX, fovWidth)) : fovWidth / 2.0;
            double centerY = Config.UseCustomCenter ? Math.Max(0, Math.Min(Config.CenterY, fovHeight)) : fovHeight / 2.0;
            
            // Calculate maximum radius based on center position
            double maxR = Math.Min(
                Math.Min(centerX, fovWidth - centerX),
                Math.Min(centerY, fovHeight - centerY)
            );

            // Draw rings based on radius mode
            if (Config.RadiusMode == RingRadiusMode.ByPixelRadius)
            {
                // Use pixel radius values
                foreach (var radius in Config.PixelRadii)
                {
                    if (radius > 0 && radius <= maxR - ringWidth / 2.0)
                    {
                        Cv2.Circle(ring, new Point(centerX, centerY), radius, Config.AltBrush.ToScalar(), ringWidth, LineTypes.AntiAlias);
                    }
                }
            }
            else // ByRadiusRatio
            {
                // Use ratio coefficients
                foreach (var ratio in Config.RadiusRatios)
                {
                    if (ratio > 0 && ratio <= 1.0)
                    {
                        int radius = (int)Math.Round(maxR * ratio);
                        if (radius <= maxR - ringWidth / 2.0)
                        {
                            Cv2.Circle(ring, new Point(centerX, centerY), radius, Config.AltBrush.ToScalar(), ringWidth, LineTypes.AntiAlias);
                        }
                    }
                }
            }

            // Draw center lines
            if (Config.DrawCenterLine)
            {
                int centerXInt = (int)Math.Round(centerX);
                int centerYInt = (int)Math.Round(centerY);
                int lineWidth = Math.Max(1, Config.CenterLineWidth);
                
                // Horizontal line
                Cv2.Line(ring, new Point(0, centerYInt), new Point(fovWidth - 1, centerYInt), Config.AltBrush.ToScalar(), lineWidth);
                // Vertical line
                Cv2.Line(ring, new Point(centerXInt, 0), new Point(centerXInt, fovHeight - 1), Config.AltBrush.ToScalar(), lineWidth);
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
