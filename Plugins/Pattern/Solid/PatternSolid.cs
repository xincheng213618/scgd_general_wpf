using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Solid
{

    public class PatternSolodConfig:ViewModelBase,IConfig
    {


        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.White;

        public string Tag { get => _Tag; set { _Tag = value; OnPropertyChanged(); } }
        private string _Tag = "W";

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

    [DisplayName("纯色")]
    public class PatternSolid : IPatternBase<PatternSolodConfig>
    {
        public override UserControl GetPatternEditor() => new SolidEditor(Config);


        public override string GetTemplateName()
        {
            string baseName = "Solid" + "_" + Config.Tag;
            
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

            // If dimensions match the entire image, just return a solid color mat
            if (fovWidth == width && fovHeight == height)
            {
                return new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            }
            else
            {
                // Create background mat
                var mat = new Mat(height, width, MatType.CV_8UC3, Config.BackGroundBrush.ToScalar());

                // Calculate center position
                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                // Create center region with main color
                Mat centerRegion = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());
                centerRegion.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                centerRegion.Dispose();
                return mat;
            }
        }

    }
}
