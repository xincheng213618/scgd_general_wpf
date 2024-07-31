using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    /// <summary>
    /// EditLEDStripDetection.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateThird : UserControl
    {
        public EditTemplateThird()
        {
            InitializeComponent();
        }
        public ModThirdPartyParam Param { get; set; }

        public void SetParam(ModThirdPartyParam param)
        {
            Param = param;
            this.DataContext = Param;
        }
    }
}
