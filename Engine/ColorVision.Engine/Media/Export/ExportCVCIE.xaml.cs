using ColorVision.FileIO;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Media
{


    /// <summary>
    /// ExportCVCIE.xaml 的交互逻辑
    /// </summary>
    public partial class ExportCVCIE : Window
    {
        private static readonly CompositeFormat SavedSuccessfullyMessage =
            CompositeFormat.Parse(ColorVision.Engine.Properties.Resources.Engine_Msg_SavedSuccessfully);
        private static readonly CompositeFormat ExportFailedMessage =
            CompositeFormat.Parse(ColorVision.Engine.Properties.Resources.ExportFailedMessage);

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
            if (!VExportCIE.AvailableImageFormats.Values.Any(format => format.Guid == VExportCIE.ExportImageFormat.Guid))
                VExportCIE.ExportImageFormat = VExportCIE.AvailableImageFormats.First().Value;
            VExportCIE.PrepareForInteractiveExport();
            Title = $"{ColorVision.Engine.Properties.Resources.Export} - {VExportCIE.FileKindName}";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
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

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ExportButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            ExportForm.IsEnabled = false;
            ExportProgress.Visibility = Visibility.Visible;
            bool succeeded = false;
            try
            {
                string savePath = VExportCIE.ResolveSaveDirectory(VExportCIE.SavePath, VExportCIE.FilePath);
                VExportCIE.SavePath = savePath;
                VExportCIE.RememberExportLocation(savePath);
                await Task.Run(() => VExportCIE.SaveToTifOrThrow(VExportCIE));
                MessageBox.Show(
                    this,
                    string.Format(CultureInfo.CurrentCulture, SavedSuccessfullyMessage, savePath),
                    ColorVision.Engine.Properties.Resources.Export,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                succeeded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    string.Format(CultureInfo.CurrentCulture, ExportFailedMessage, ex.Message),
                    ColorVision.Engine.Properties.Resources.Export,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ExportButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                ExportForm.IsEnabled = true;
                ExportProgress.Visibility = Visibility.Collapsed;
            }

            if (succeeded)
                Close();
        }
    }
}
