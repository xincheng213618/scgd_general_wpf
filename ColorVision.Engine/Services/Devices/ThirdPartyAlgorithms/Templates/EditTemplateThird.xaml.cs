using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    /// <summary>
    /// EditTemplateThird.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateThird : UserControl
    {
        public EditTemplateThird()
        {
            InitializeComponent();
        }
        public FindDotsArrayParam Param { get; set; }

        public void SetParam(FindDotsArrayParam param)
        {
            Param = param;
            this.DataContext = Param;
        }
    }
}
