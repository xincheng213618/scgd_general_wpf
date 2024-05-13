using ColorVision.Net;
using ColorVision.Solution.V.Files;
using ColorVision.Common.Utilities;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using ColorVision.Services.Export;
namespace ColorVision.Services.Export
{


    /// <summary>
    /// ExportCVCIE.xaml 的交互逻辑
    /// </summary>
    public partial class ExportCVCIE : Window
    {

        public string CIEFilePath { get; set; }
        public VExportCIE VExportCIE { get; set; }

        public ExportCVCIE(string cIEFilePath)
        {
            VExportCIE = new VExportCIE(cIEFilePath);
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (!CVFileUtil.IsCIEFile(VExportCIE.FilePath))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "导出仅支持CIE文件", "ColorVision");
                return;
            }
            DataContext = VExportCIE;

        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                VExportCIE.SavePath = dialog.SelectedPath;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Thread thread = new(() => VExportCIE.SaveToTif(VExportCIE));
            thread.Start();
            Close();
        }
    }
}
