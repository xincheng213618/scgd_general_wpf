#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.ImageEditor.Tif
{
    public class FileProcessorTif : IFileProcessor
    {
        public int Order => -1;

        public bool CanExport(string filePath)
        {
            return false;
        }

        public bool CanProcess(string filePath)
        {
            return true;
        }

        public void Export(string filePath)
        {
        }

        public void Process(string filePath)
        {
            ImageView imageView = new ImageView();
            Window window = new() { Title = "快速预览" };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(filePath);
            window.Show();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ImageViewModel.ClearImage();
                }));
            }
        }
    }

}
