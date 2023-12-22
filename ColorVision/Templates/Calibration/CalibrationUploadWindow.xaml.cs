using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ColorVision.Device.Camera;
using ColorVision.MySql.DAO;
using ColorVision.Themes.Controls;
using cvColorVision;

namespace ColorVision.Templates
{
    /// <summary>
    /// CalibrationUploadWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationUploadWindow : Window
    {
        public DeviceServiceCamera DeviceServiceCamera { get; set; }

        public ResouceType ResouceType { get; set; }

        public CalibrationUploadWindow(DeviceServiceCamera deviceServiceCamera, ResouceType resouceType) :this()
        {
            DeviceServiceCamera = deviceServiceCamera;
            ResouceType = resouceType;
        }

        public CalibrationUploadWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        public EventHandler OnUpload { get; set; }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Upload1.UploadFileName)|| string.IsNullOrEmpty(Upload1.UploadFilePath))
            {
                MessageBox.Show("您未选择文件");
                this.Close();
                return;
            }
            OnUpload?.Invoke(Upload1,new EventArgs());
            this.Close();
        }
    }
}
