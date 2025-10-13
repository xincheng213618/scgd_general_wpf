using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.ImageEditor.Tif
{
    public class FileProcessorTif : IFileProcessor
    {
        public int Order => -1;

        public void Export(string filePath)
        {
        }

        public void Process(string filePath)
        {
            ImageView imageView = new ImageView();
            Window window = new() { Title = filePath };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(filePath);

            imageView.ImageShow.ImageInitialized += (s, e) =>
            {
                window.Title = filePath;
            };
            window.ApplyCaption();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ImageViewModel.ClearImage();
                }));
            }
            window.Show();

        }
    }

}
