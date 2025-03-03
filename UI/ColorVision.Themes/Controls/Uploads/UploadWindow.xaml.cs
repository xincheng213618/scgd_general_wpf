using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Themes.Controls.Uploads
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
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Filter))
                Upload1.Filter = Filter;
            Upload1.SelectChaned += (s, e) =>
            {
                ButtonUpload.Visibility = Visibility.Visible;
            };
            Task.Run(() => LoadImageAsync());
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    this.Close();
                }
                else if (e.Key == Key.Enter)
                {
                    if (ButtonUpload.Visibility == Visibility.Visible)
                    {
                        if (string.IsNullOrEmpty(Upload1.UploadFileName) || string.IsNullOrEmpty(Upload1.UploadFilePath))
                        {
                            MessageBox.Show("您未选择文件");
                            Close();
                            return;
                        }
                        OnUpload?.Invoke(this, Upload1);
                        Close();
                    }
                    else
                    {
                        Upload1.ChoiceFile();
                    }
                }
            };
        }
        private async void LoadImageAsync()
        {            
            var imageSource = await Task.Run(() =>
            {
                return new BitmapImage(new Uri("/ColorVision.Themes;component/Assets/Image/uploadbg.avif", UriKind.Relative));
            });
            _ = Application.Current.Dispatcher.BeginInvoke(() =>
            {
                backImage.Source = imageSource;
            });

        }

            private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        public EventHandler<UploadControl> OnUpload { get; set; }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Upload1.UploadFileName)|| string.IsNullOrEmpty(Upload1.UploadFilePath))
            {
                MessageBox.Show("您未选择文件");
                Close();
                return;
            }
            OnUpload?.Invoke(this,Upload1);
            Close();
        }
    }
}
