using ColorVision.ImageEditor;
using ColorVision.UI;
using System.ComponentModel;
using System.Windows;


namespace ColorVision.Solution.FileMeta
{
    [FileExtension(".jpg|.png|.jpeg|.tif|.bmp|.tiff|.cvraw|.cvcie", "图片编辑器")]
    public class FileProcessorImage : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public bool Process(string filePath)
        {
            ImageView imageView = new();
            Window window = new() { Title =filePath };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(filePath);
            imageView.ImageShow.ImageInitialized += (s, e) =>
            {
                window.Title = $"{filePath} - {imageView.ImageShow.Source.Width}x{imageView.ImageShow.Source.Height}";
            };
            window.Show();

            return true;

        }
    }



}
