using System;
using System.Windows;

namespace ColorVision.Services.Devices
{
    /// <summary>
    /// UploadWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UploadWindow : Window
    {
        public string Filter { get; set; }
        public UploadWindow(string Filter = "")
        {
            this.Filter = Filter;
            InitializeComponent();
        }

       

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Filter))
                Upload1.Filter = Filter;
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
