using ColorVision.UI.Menus;
using Spectrum.Menus;
using System.Windows;

namespace Spectrum.Calibration
{
    public class MenuGenerateAmplitudeWindow : SpectrumMenuIBase
    {

        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "生成幅值标定文件";
        public override int Order => 1;
        public override void Execute()
        {
            new GenerateAmplitudeWindow().ShowDialog();
        }
    }


    public partial class GenerateAmplitudeWindow : Window
    {
        public GenerateAmplitudeWindow()
        {
            InitializeComponent();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SpectrometerManager.Instance;
        }
    }
}
