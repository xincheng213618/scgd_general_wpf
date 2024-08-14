using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2
{
    /// <summary>
    /// EditLEDStripDetection.xaml 的交互逻辑
    /// </summary>
    public partial class EditLedCheck2 : UserControl
    {
        public EditLedCheck2()
        {
            InitializeComponent();
        }

        public LedCheck2Param Param { get; set; }

        public void SetParam(LedCheck2Param param)
        {
            Param = param;
            this.DataContext = Param;
        }

    }
}
