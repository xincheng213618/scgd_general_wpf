using ColorVision.UI;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{

    public class EditTemplateThirdConfig :IConfig
    {
        public static EditTemplateThirdConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateThirdConfig>();

        public double Width { get; set; } = double.NaN;


    }

    /// <summary>
    /// EditSFR.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateThird : UserControl
    {
        public EditTemplateThird()
        {
            InitializeComponent();
            this.Width = EditTemplateThirdConfig.Instance.Width;
            this.SizeChanged += (s, e) =>
            {
                EditTemplateThirdConfig.Instance.Width = this.ActualWidth;
            };

        }
        public ModThirdPartyParam Param { get; set; }

        public void SetParam(ModThirdPartyParam param)
        {
            Param = param;
            this.DataContext = Param;
        }
    }
}
