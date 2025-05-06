using ColorVision.Net;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Media
{


    /// <summary>
    /// ExportCVCIE.xaml 的交互逻辑
    /// </summary>
    public partial class ExportCVCIE : Window
    {

        public string FilePath { get; set; }
        public VExportCIE VExportCIE { get; set; }

        public ExportCVCIE(string filePath)
        {
            VExportCIE = new VExportCIE(filePath);
            InitializeComponent();
        }

        public ExportCVCIE(VExportCIE  vExportCIE)
        {
            VExportCIE = vExportCIE;
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
            var imageFormats = new Dictionary<string, ImageFormat>
            {
                { "TIFF Image (*.tiff)", ImageFormat.Tiff },
                { "Bitmap Image (*.bmp)", ImageFormat.Bmp },
                { "PNG Image (*.png)", ImageFormat.Png },
                { "JPEG Image (*.jpg;*.jpeg)", ImageFormat.Jpeg },
            };
            ExportImageFormatComboBox.ItemsSource = imageFormats;
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
                VExportCIE.RecentImage.InsertFile(VExportCIE.SavePath);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            VExportCIE.RecentImage.InsertFile(VExportCIE.SavePath);
            Thread thread = new(() => VExportCIE.SaveToTif(VExportCIE));
            thread.Start();
            Close();
        }
    }
}
