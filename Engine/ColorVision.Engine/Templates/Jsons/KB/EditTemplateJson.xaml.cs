using ColorVision.UI;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{

    public class EditKBTemplateJsonConfig :IConfig
    {
        public static EditKBTemplateJsonConfig Instance => ConfigService.Instance.GetRequiredService<EditKBTemplateJsonConfig>();

        public double Width { get; set; } = double.NaN;
    }

    public partial class EditKBTemplateJson : UserControl, ITemplateUserControl
    {
        public EditKBTemplateJson()
        {
            InitializeComponent();
            this.Width = EditKBTemplateJsonConfig.Instance.Width;
            this.SizeChanged += (s, e) =>
            {
                EditKBTemplateJsonConfig.Instance.Width = this.ActualWidth;
            };
        }

        public void SetParam(object param)
        {
            this.DataContext = param;
        }
    }
}
