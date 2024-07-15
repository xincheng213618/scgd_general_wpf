using ColorVision.Common.Utilities;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Net;
using ColorVision.UI;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;

namespace ColorVision.Engine.Impl.FileProcessor
{
    public class CVCIEFile : IFileProcessor
    {
        public int Order => 2;

        public bool CanProcess(string filePath)
        {
            return filePath.EndsWith("cvcie", StringComparison.OrdinalIgnoreCase);
        }

        public void Process(string filePath)
        {
            ImageView imageView = new();
            CVFileUtil.ReadCVRaw(filePath, out Net.CVCIEFile fileInfo);
            Window window = new() { Title = Properties.Resources.QuickPreview };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(new NetFileUtil().OpenLocalCVFile(filePath, FileExtType.CIE).ToWriteableBitmap());
            window.Show();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ToolBarTop.ClearImage();
                }));
            }
        }
    }


}
