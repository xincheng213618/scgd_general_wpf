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

        public bool CanProcess(string filePath)
        {
            return true;
        }
        public void Export(string filePath)
        {

        }
        public bool CanExport(string filePath)
        {
            return true;
        }

        public void Process(string filePath)
        {
            ImageView imageView = new();
            Window window = new() { };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(filePath);

            window.Show();
        }
    }



}
