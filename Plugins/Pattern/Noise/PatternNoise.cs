using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Noise
{
    public enum NoiseType
    {
        [Description("高斯噪声")]
        Gaussian,
        [Description("椒盐噪声")]
        SaltAndPepper,
        [Description("均匀噪声")]
        Uniform
    }

    public enum SolidSizeMode
    {
        ByFieldOfView,
        ByPixelSize
    }

    public class PatternNoiseConfig : ViewModelBase, IConfig
    {
        [DisplayName("噪声类型")]
        public NoiseType NoiseType { get => _NoiseType; set { _NoiseType = value; OnPropertyChanged(); } }
        private NoiseType _NoiseType = NoiseType.Gaussian;

        [DisplayName("背景色")]
        public SolidColorBrush BackgroundBrush { get => _BackgroundBrush; set { _BackgroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackgroundBrush = Brushes.Black;

        [DisplayName("噪声颜色")]
        public SolidColorBrush NoiseBrush { get => _NoiseBrush; set { _NoiseBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _NoiseBrush = Brushes.White;

        [DisplayName("噪声强度")]
        [Description("高斯噪声的标准差 (0-255)")]
        public double Intensity { get => _Intensity; set { _Intensity = value; OnPropertyChanged(); } }
        private double _Intensity = 25.0;

        [DisplayName("噪声密度")]
        [Description("椒盐噪声的像素比例 (0.0-1.0)")]
        public double Density { get => _Density; set { _Density = value; OnPropertyChanged(); } }
        private double _Density = 0.05;

        [DisplayName("随机种子")]
        [Description("用于生成可重复的噪声 (-1 表示随机)")]
        public int RandomSeed { get => _RandomSeed; set { _RandomSeed = value; OnPropertyChanged(); } }
        private int _RandomSeed = -1;

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

    [DisplayName("噪点")]
    public class PatternNoise : IPatternBase<PatternNoiseConfig>
    {
        public override UserControl GetPatternEditor() => new NoiseEditor(Config);

        public override string GetTemplateName()
        {
            string baseName = $"Noise_{Config.NoiseType}";

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

            // Create base image with background color
            Mat noiseMat = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.BackgroundBrush.ToScalar());

            // Set random seed if specified
            Random random;
            if (Config.RandomSeed >= 0)
            {
                random = new Random(Config.RandomSeed);
            }
            else
            {
                random = new Random();
            }

            // Generate noise based on type
            switch (Config.NoiseType)
            {
                case NoiseType.Gaussian:
                    GenerateGaussianNoise(noiseMat, random);
                    break;
                case NoiseType.SaltAndPepper:
                    GenerateSaltAndPepperNoise(noiseMat, random);
                    break;
                case NoiseType.Uniform:
                    GenerateUniformNoise(noiseMat, random);
                    break;
            }

            // If dimensions match the entire image, return directly
            if (fovWidth == width && fovHeight == height)
            {
                return noiseMat;
            }
            else
            {
                // Create background mat and paste noise in center
                Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.BackgroundBrush.ToScalar());
                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                noiseMat.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                noiseMat.Dispose();
                return mat;
            }
        }

        private void GenerateGaussianNoise(Mat mat, Random random)
        {
            // Get background and noise colors
            var bgColor = Config.BackgroundBrush.ToScalar();
            double intensity = Math.Max(0, Math.Min(Config.Intensity, 255));

            // Generate Gaussian noise for each pixel
            for (int y = 0; y < mat.Height; y++)
            {
                for (int x = 0; x < mat.Width; x++)
                {
                    // Generate Gaussian random values for each channel
                    double noise_b = GenerateGaussianRandom(random) * intensity;
                    double noise_g = GenerateGaussianRandom(random) * intensity;
                    double noise_r = GenerateGaussianRandom(random) * intensity;

                    // Add noise to background color and clamp
                    byte b = (byte)Math.Max(0, Math.Min(255, bgColor.Val0 + noise_b));
                    byte g = (byte)Math.Max(0, Math.Min(255, bgColor.Val1 + noise_g));
                    byte r = (byte)Math.Max(0, Math.Min(255, bgColor.Val2 + noise_r));

                    mat.Set(y, x, new Vec3b(b, g, r));
                }
            }
        }

        private void GenerateSaltAndPepperNoise(Mat mat, Random random)
        {
            double density = Math.Max(0, Math.Min(Config.Density, 1.0));
            var noiseColor = Config.NoiseBrush.ToScalar();

            // Apply salt and pepper noise
            for (int y = 0; y < mat.Height; y++)
            {
                for (int x = 0; x < mat.Width; x++)
                {
                    if (random.NextDouble() < density)
                    {
                        // Randomly choose between salt (white) or pepper (noise color)
                        if (random.NextDouble() < 0.5)
                        {
                            // Salt - white
                            mat.Set(y, x, new Vec3b(255, 255, 255));
                        }
                        else
                        {
                            // Pepper - noise color
                            mat.Set(y, x, new Vec3b(
                                (byte)noiseColor.Val0,
                                (byte)noiseColor.Val1,
                                (byte)noiseColor.Val2));
                        }
                    }
                }
            }
        }

        private void GenerateUniformNoise(Mat mat, Random random)
        {
            double intensity = Math.Max(0, Math.Min(Config.Intensity, 255));
            var bgColor = Config.BackgroundBrush.ToScalar();

            // Generate uniform noise for each pixel
            for (int y = 0; y < mat.Height; y++)
            {
                for (int x = 0; x < mat.Width; x++)
                {
                    // Generate uniform random values in range [-intensity, +intensity]
                    double noise_b = (random.NextDouble() * 2 - 1) * intensity;
                    double noise_g = (random.NextDouble() * 2 - 1) * intensity;
                    double noise_r = (random.NextDouble() * 2 - 1) * intensity;

                    // Add noise to background color and clamp
                    byte b = (byte)Math.Max(0, Math.Min(255, bgColor.Val0 + noise_b));
                    byte g = (byte)Math.Max(0, Math.Min(255, bgColor.Val1 + noise_g));
                    byte r = (byte)Math.Max(0, Math.Min(255, bgColor.Val2 + noise_r));

                    mat.Set(y, x, new Vec3b(b, g, r));
                }
            }
        }

        // Box-Muller transform to generate Gaussian distributed random numbers
        private double GenerateGaussianRandom(Random random)
        {
            double u1 = random.NextDouble();
            double u2 = random.NextDouble();
            
            // Avoid log(0) by ensuring u1 is not zero
            if (u1 < 1e-10)
                u1 = 1e-10;
            
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
