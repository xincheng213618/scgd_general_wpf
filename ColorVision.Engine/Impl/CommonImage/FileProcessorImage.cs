using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.Impl.CommonImage
{
    public class FileProcessorImage : IFileProcessor
    {
        public int Order => 3;

        public bool CanProcess(string filePath)
        {
            return Tool.IsImageFile(filePath);
        }
        public void Process(string filePath)
        {
            ImageView imageView = new();
            Window window = new() { Title = "快速预览" };
            window.Content = imageView;
            imageView.OpenImage(filePath);
            window.Show();
        }
    }


}
