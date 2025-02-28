using ColorVision.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons
{

    public class EditTemplateJsonConfig :IConfig
    {
        public static EditTemplateJsonConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateJsonConfig>();

        public double Width { get; set; } = double.NaN;
    }

    public partial class EditTemplateJson : UserControl, ITemplateUserControl
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

        public void SetParam(object param)
        {
            this.DataContext = param;
        }
    }
}
