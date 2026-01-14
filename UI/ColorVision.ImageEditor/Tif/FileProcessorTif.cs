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

        public bool Process(string filePath)
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
                window.Title = $"{imageView.Config.FilePath} - {imageView.ImageShow.Source.Width}x{imageView.ImageShow.Source.Height} {imageView.Config.GetProperties<int>("Channel")}";
            };
            window.ApplyCaption();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.Clear();
                }));
            }
            window.Show();

            return true;

        }
    }

}
