using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.UI;
using System;
using System.Windows;

namespace ColorVision.Engine.Impl.FileProcessor
{
    public class FileProcessorCVCIE : IFileProcessor
    {
        public string GetExtension() => "cvcie|*.cvcie"; // "cvcie

        public int Order => 2;

        public bool CanProcess(string filePath)
        {
            return filePath.EndsWith("cvcie", StringComparison.OrdinalIgnoreCase);
        }

        public void Process(string filePath)
        {
            ImageView imageView = new();
            Window window = new() { Title = Properties.Resources.QuickPreview };
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
                    imageView.ImageEditViewMode.ClearImage();
                }));
            }
        }
        public bool CanExport(string filePath)
        {
            return filePath.EndsWith("cvcie", StringComparison.OrdinalIgnoreCase);
        }
        public void Export(string filePath)
        {
            new ExportCVCIE(filePath).Show();
        }


    }


}
