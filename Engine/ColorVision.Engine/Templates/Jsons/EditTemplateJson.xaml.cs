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

        private void OpenEdit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // 获取程序运行路径
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            // 相对文件路径
            string relativePath = @"Assets/Tool/EditJson/Editjson.html";

            // 合并路径并获取绝对路径
            string absolutePath = Path.Combine(basePath, relativePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = absolutePath,
                UseShellExecute = true // 使用默认应用程序打开
            });
        }
    }
}
