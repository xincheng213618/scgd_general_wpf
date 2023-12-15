using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ColorVision.Device.Camera;
using ColorVision.MySql.DAO;
using cvColorVision;

namespace ColorVision.Templates
{
    /// <summary>
    /// CalibrationUpload.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationUpload : Window
    {
        public DeviceServiceCamera DeviceServiceCamera { get; set; }

        public ResouceType ResouceType { get; set; }

        public CalibrationUpload(DeviceServiceCamera deviceServiceCamera, ResouceType resouceType) :this()
        {
            DeviceServiceCamera = deviceServiceCamera;
            ResouceType = resouceType;
        }



        public CalibrationUpload()
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




        private void Window_Initialized(object sender, EventArgs e)
        {
            CobCalibration.ItemsSource = Enum.GetValues(typeof(CalibrationType)).Cast<CalibrationType>();
            CobCalibration.SelectedIndex = 0;

        }
        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            UploadRec.Stroke = Brushes.Gray;
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                TxtCalibrationFile.Text = a?.First();
                TxtCalibrationFileName.Text = Path.GetFileName(a?.First());
            }
        }

        private void UIElement_OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            openFileDialog.Filter = "All files (*.*)|*.*";
            openFileDialog.Multiselect = false;

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                TxtCalibrationFile.Text = openFileDialog.FileName;
                TxtCalibrationFileName.Text = openFileDialog.SafeFileName;
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DeviceServiceCamera.UploadCalibrationFile("", TxtCalibrationFile.Text, (int)ResouceType);
        }
    }
}
