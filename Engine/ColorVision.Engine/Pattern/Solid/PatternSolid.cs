using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern.Solid
{
    public class PatternSolodConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.White;
    }

    [DisplayName("纯色")]
    public class PatternSolid : IPattern
    {
        public static PatternSolodConfig Config => ConfigService.Instance.GetRequiredService<PatternSolodConfig>();
        public ViewModelBase GetConfig() => Config;

        public Mat Gen(int height, int width)
        {
            return new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
        }

        public UserControl GetPatternEditor() => new SolidEditor();
    }
}
