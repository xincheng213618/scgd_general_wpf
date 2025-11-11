using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Stripe
{
    public enum CheckerboardSizeMode
    {
        ByGridCount,
        ByCellSize
    }

    public enum SolidSizeMode
    {
        ByFieldOfView,
        ByPixelSize
    }

    public class PatternStripeConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;

        public bool IsHorizontal { get => _IsHorizontal; set { _IsHorizontal = value; OnPropertyChanged(); } }
        private bool _IsHorizontal = true;

        // 横线
        public int HorizontalSpacing { get => _HorizontalSpacing; set { _HorizontalSpacing = value; OnPropertyChanged(); } }
        private int _HorizontalSpacing = 2;

        public int HorizontalWidth { get => _HorizontalWidth; set { _HorizontalWidth = value; OnPropertyChanged(); } }
        private int _HorizontalWidth = 1;

        // 竖线
        public int VerticalSpacing { get => _VerticalSpacing; set { _VerticalSpacing = value; OnPropertyChanged(); } }
        private int _VerticalSpacing = 2;

        public int VerticalWidth { get => _VerticalWidth; set { _VerticalWidth = value; OnPropertyChanged(); } }
        private int _VerticalWidth = 1;


        public string MainBrushTag { get => _MainBrushTag; set { _MainBrushTag = value; OnPropertyChanged(); } }
        private string _MainBrushTag = "K";
        
        public string AltBrushTag { get => _AltBrushTag; set { _AltBrushTag = value; OnPropertyChanged(); } }
        private string _AltBrushTag = "W";

        public SolidSizeMode SizeMode { get => _SizeMode; set { _SizeMode = value; OnPropertyChanged(); } }
        private SolidSizeMode _SizeMode = SolidSizeMode.ByFieldOfView;

        public double FieldOfViewX { get => _FieldOfViewX; set { _FieldOfViewX = value; OnPropertyChanged(); } }
        private double _FieldOfViewX = 1.0;
        public double FieldOfViewY { get => _FieldOfViewY; set { _FieldOfViewY = value; OnPropertyChanged(); } }
        private double _FieldOfViewY = 1.0;

        public int PixelWidth { get => _PixelWidth; set { _PixelWidth = value; OnPropertyChanged(); } }
        private int _PixelWidth = 100;
        public int PixelHeight { get => _PixelHeight; set { _PixelHeight = value; OnPropertyChanged(); } }
        private int _PixelHeight = 100;

    }

    /// <summary>
    /// https://stackoverflow.com/questions/24682797/python-opencv-drawing-line-width
    /// </summary>
    [DisplayName("隔行点亮")]
    public class PatternStripe : IPatternBase<PatternStripeConfig>
    {
        public override UserControl GetPatternEditor() => new StripeEditor(Config);


        public override string GetTemplateName()
        {
            string baseName = "Stripe" + "_" + Config.MainBrushTag + Config.AltBrushTag + "_" + 
                (Config.IsHorizontal ? $"H_{Config.HorizontalSpacing}_{Config.HorizontalWidth}" : $"V_{Config.VerticalSpacing}_{Config.VerticalWidth}");
            
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

            // Generate stripe pattern
            Mat stripe = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            if (Config.IsHorizontal)
            {
                int hSpacing = Math.Max(1, Config.HorizontalSpacing);
                int hWidth = Math.Max(1, Config.HorizontalWidth);

                for (int y = 0; y < fovHeight; y += hSpacing)
                {
                    for (int dy = 0; dy < hWidth; dy++)
                    {
                        if (y + dy < fovHeight)
                            stripe.Row(y + dy).SetTo(Config.AltBrush.ToScalar());
                    }
                }
            }
            else
            {
                int vSpacing = Math.Max(1, Config.VerticalSpacing);
                int vWidth = Math.Max(1, Config.VerticalWidth);

                for (int x = 0; x < fovWidth; x += vSpacing)
                {
                    for (int dx = 0; dx < vWidth; dx++)
                    {
                        if (x + dx < fovWidth)
                            stripe.Col(x + dx).SetTo(Config.AltBrush.ToScalar());
                    }
                }
            }

            // If dimensions match the entire image, return directly
            if (fovWidth == width && fovHeight == height)
            {
                return stripe;
            }
            else
            {
                // Create background mat and paste stripe in center
                var mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                stripe.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                stripe.Dispose();
                return mat;
            }
        }
    }
}
