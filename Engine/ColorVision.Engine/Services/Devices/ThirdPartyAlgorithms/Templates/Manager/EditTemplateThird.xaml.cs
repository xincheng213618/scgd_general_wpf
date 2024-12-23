using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.Manager
{
    /// <summary>
    /// EditSFR.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateThirdManager : UserControl
    {
        public EditTemplateThirdManager()
        {
            InitializeComponent();
        }
        public ModThirdPartyManagerParam Param { get; set; }

        public void SetParam(ModThirdPartyManagerParam param)
        {
            Param = param;
            this.DataContext = Param;
        }
    }
}
