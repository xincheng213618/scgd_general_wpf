using ColorVision.FileIO;
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
                MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExportSupportsCieFilesOnly, "ColorVision");
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
            dialog.Description = ColorVision.Engine.Properties.Resources.Engine_Dlg_SelectProjectLocation;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show(ColorVision.Engine.Properties.Resources.FolderPathCannotBeEmpty, ColorVision.Engine.Properties.Resources.Engine_Msg_Prompt);
                    return;
                }
                VExportCIE.SavePath = dialog.SelectedPath;
                VExportCIE.RememberExportLocation(VExportCIE.SavePath);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            VExportCIE.RememberExportLocation(VExportCIE.SavePath);
            Thread thread = new(() => VExportCIE.SaveToTif(VExportCIE));
            thread.Start();
            Close();
        }
    }
}
