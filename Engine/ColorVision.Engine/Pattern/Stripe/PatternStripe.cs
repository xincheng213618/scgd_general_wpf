using ColorVision.Common.MVVM;
using ColorVision.Engine.Pattern.Solid;
using ColorVision.UI;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern.Stripe
{
    public enum CheckerboardSizeMode
    {
        ByGridCount,
        ByCellSize
    }
    public class PatternStripeConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.White;

        public bool IsHorizontal { get => _IsHorizontal; set { _IsHorizontal = value; NotifyPropertyChanged(); } }
        private bool _IsHorizontal = true;

        // 横线
        public int HorizontalSpacing { get => _HorizontalSpacing; set { _HorizontalSpacing = value; NotifyPropertyChanged(); } }
        private int _HorizontalSpacing = 2;

        public int HorizontalWidth { get => _HorizontalWidth; set { _HorizontalWidth = value; NotifyPropertyChanged(); } }
        private int _HorizontalWidth = 1;

        // 竖线
        public int VerticalSpacing { get => _VerticalSpacing; set { _VerticalSpacing = value; NotifyPropertyChanged(); } }
        private int _VerticalSpacing = 2;

        public int VerticalWidth { get => _VerticalWidth; set { _VerticalWidth = value; NotifyPropertyChanged(); } }
        private int _VerticalWidth = 1;
    }

    /// <summary>
    /// https://stackoverflow.com/questions/24682797/python-opencv-drawing-line-width
    /// </summary>
    [DisplayName("隔行点亮")]
    public class PatternStripe : IPatternBase<PatternStripeConfig>
    {
        public static PatternStripeConfig Config => ConfigService.Instance.GetRequiredService<PatternStripeConfig>();
        public override ViewModelBase GetConfig() => Config;

        public override UserControl GetPatternEditor() => new StripeEditor();
        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            if (Config.IsHorizontal)
            {
                // 横线
                int hSpacing = Math.Max(1, Config.HorizontalSpacing);
                int hWidth = Math.Max(1, Config.HorizontalWidth);

                for (int y = 0; y < height; y += hSpacing)
                {
                    for (int dy = 0; dy < hWidth; dy++)
                    {
                        if (y + dy < height)
                            mat.Row(y + dy).SetTo(Config.AltBrush.ToScalar());
                    }
                }
            }
            else
            {
                // 竖线
                int vSpacing = Math.Max(1, Config.VerticalSpacing);
                int vWidth = Math.Max(1, Config.VerticalWidth);

                for (int x = 0; x < width; x += vSpacing)
                {
                    for (int dx = 0; dx < vWidth; dx++)
                    {
                        if (x + dx < width)
                            mat.Col(x + dx).SetTo(Config.AltBrush.ToScalar());
                    }
                }
            }
            return mat;
        }
    }
}
