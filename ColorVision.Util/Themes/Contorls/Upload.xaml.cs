using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Themes.Controls
{
    /// <summary>
    /// Upload.xaml 的交互逻辑
    /// </summary>
    public partial class Upload : UserControl
    {
        public Upload()
        {
            InitializeComponent();

            this.DragEnter += (s, e) =>
            {
                e.Effects = DragDropEffects.Scroll;
                e.Handled = true;
                UploadRec.Stroke = Brushes.Blue;
            };
            this.DragLeave += (s, e) =>
            {
                UploadRec.Stroke = Brushes.Gray;
            };
        }   

        public string UploadFileName
        {
            get { return (string)GetValue(UploadFileNameProperty); }
            set { SetValue(UploadFileNameProperty, value); }
        }
        public static readonly DependencyProperty UploadFileNameProperty = DependencyProperty.Register(nameof(UploadFileNameProperty), typeof(string), typeof(Upload), new PropertyMetadata(""));

        public string UploadFilePath
        {
            get { return (string)GetValue(UploadFilePathProperty); }
            set { SetValue(UploadFilePathProperty, value); }
        }
        public static readonly DependencyProperty UploadFilePathProperty  = DependencyProperty.Register(nameof(UploadFilePathProperty), typeof(string), typeof(Upload), new PropertyMetadata(""));


        private void ChoiceFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            openFileDialog.Filter = "All files (*.*)|*.*";
            openFileDialog.Multiselect = false;

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                TxtCalibrationFile.Text = openFileDialog.FileName;
                TxtCalibrationFileName.Text = openFileDialog.SafeFileName;
                UploadFileName = TxtCalibrationFileName.Text;
                UploadFilePath = TxtCalibrationFile.Text;
                GridUpdate.Visibility = Visibility.Collapsed;
                GridShow.Visibility = Visibility.Visible;
                UploadRec.Stroke = Brushes.Gray;
            }
        }


        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                TxtCalibrationFile.Text = a?.First();
                TxtCalibrationFileName.Text = Path.GetFileName(a?.First());
                UploadFileName = TxtCalibrationFileName.Text ?? string.Empty;
                UploadFilePath = TxtCalibrationFile.Text ?? string.Empty;
                GridUpdate.Visibility = Visibility.Collapsed;
                GridShow.Visibility = Visibility.Visible;
                UploadRec.Stroke = Brushes.Gray;
            }


        }
    }
}
