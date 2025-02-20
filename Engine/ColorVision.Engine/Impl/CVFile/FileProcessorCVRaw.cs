using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.UI;
using System;
using System.Windows;

namespace ColorVision.Engine.Impl.SolutionImpl
{
    public class FileProcessorCVRaw : IFileProcessor
    {
        public string GetExtension() => "cvraw|*.cvraw"; // "cvcie
        public int Order => 1;

        public bool CanProcess(string filePath)
        {
            return filePath.EndsWith("cvraw", StringComparison.OrdinalIgnoreCase);
        }
        public void Export(string filePath)
        {
            new ExportCVCIE(filePath).Show();
        }
        public bool CanExport(string filePath)
        {
            return filePath.EndsWith("cvraw", StringComparison.OrdinalIgnoreCase);
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
                    imageView.ImageViewModel.ClearImage();
                }));
            }
        }
    }


}
