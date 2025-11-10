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

        public string Tag { get; set; } = "W";

        [DisplayName("视场背景")]
        public SolidColorBrush BackGroundBrush { get => _BackGroundBrush; set { _BackGroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackGroundBrush = Brushes.Black;
        public double FieldOfViewX { get => _FieldOfViewX; set { _FieldOfViewX = value; OnPropertyChanged(); } }
        private double _FieldOfViewX = 1.0;
        public double FieldOfViewY { get => _FieldOfViewY; set { _FieldOfViewY = value; OnPropertyChanged(); } }
        private double _FieldOfViewY = 1.0;
    }

    [DisplayName("纯色")]
    public class PatternSolid : IPatternBase<PatternSolodConfig>
    {
        public override UserControl GetPatternEditor() => new SolidEditor(Config);


        public override string GetTemplateName()
        {
            return "Solid" + "_" + Config.Tag;
        }

        public override Mat Gen(int height, int width)
        {

            if (Config.FieldOfViewX ==1 && Config.FieldOfViewY == 1)
            {
                return new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            }
            else
            {
                var mat = new Mat(height, width, MatType.CV_8UC3, Config.BackGroundBrush.ToScalar());

                // 2. 计算视场中心区域
                double fovx = Math.Max(0, Math.Min(Config.FieldOfViewX, 1.0));
                double fovy = Math.Max(0, Math.Min(Config.FieldOfViewY, 1.0));

                int fovWidth = (int)(width * fovx);
                int fovHeight = (int)(height * fovy);

                int startX = (width - fovWidth) / 2;
                int startY = (height - fovHeight) / 2;

                // 3. 生成中心棋盘格小图
                Mat checker = new Mat(fovHeight, fovWidth, MatType.CV_8UC3, Config.MainBrush.ToScalar());
                checker.CopyTo(mat[new Rect(startX, startY, fovWidth, fovHeight)]);
                checker.Dispose();
                return mat;
            }
        }

    }
}
