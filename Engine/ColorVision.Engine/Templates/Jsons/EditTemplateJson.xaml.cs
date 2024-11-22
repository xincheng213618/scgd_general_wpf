using ColorVision.UI;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons
{

    public class EditTemplateJsonConfig :IConfig
    {
        public static EditTemplateJsonConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateJsonConfig>();

        public double Width { get; set; } = double.NaN;
    }

    /// <summary>
    /// EditSFR.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateJson : UserControl
    {
        public EditTemplateJson()
        {
            InitializeComponent();
            this.Width = EditTemplateJsonConfig.Instance.Width;
            this.SizeChanged += (s, e) =>
            {
                EditTemplateJsonConfig.Instance.Width = this.ActualWidth;
            };
        }

        public IEditTemplateJson Param { get; set; }

        public void SetParam(IEditTemplateJson param)
        {
            Param = param;
            this.DataContext = Param;
        }
    }
}
