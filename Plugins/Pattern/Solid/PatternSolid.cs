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
            return new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
        }

    }
}
