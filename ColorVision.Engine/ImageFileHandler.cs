using ColorVision.Common.Extension;
using ColorVision.Common.Utilities;
using ColorVision.Media;
using ColorVision.Net;
using ColorVision.UI;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;

namespace ColorVision.Engine
{
    public class CVRawFileHandler : IFileHandler
    {
        public int Order => 1;

        public bool CanHandle(string filePath)
        {
            return filePath.EndsWith("cvraw", StringComparison.OrdinalIgnoreCase);
        }

        public void Handle(string filePath)
        {
            ImageView imageView = new();
            CVFileUtil.ReadCVRaw(filePath, out CVCIEFile fileInfo);
            Window window = new() { Title = Properties.Resources.QuickPreview };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(fileInfo);

            window.Show();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() => {
                    imageView.ToolBarTop.ClearImage();
                }));
            }
        }
    }

    public class CVCIEFileHandler : IFileHandler
    {
        public int Order => 2;

        public bool CanHandle(string filePath)
        {
            return filePath.EndsWith("cvcie", StringComparison.OrdinalIgnoreCase);
        }

        public void Handle(string filePath)
        {
            ImageView imageView = new();
            CVFileUtil.ReadCVRaw(filePath, out CVCIEFile fileInfo);
            Window window = new() { Title = Properties.Resources.QuickPreview};
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(new NetFileUtil().OpenLocalCVFile(filePath, FileExtType.CIE));
            window.Show();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() => {
                    imageView.ToolBarTop.ClearImage();
                }));
            }
        }
    }


    public class ImageFileHandler : IFileHandler
    {
        public int Order => 3;

        public bool CanHandle(string filePath)
        {
            return Tool.IsImageFile(filePath);
        }

        public void Handle(string filePath)
        {
            ImageView imageView = new();
            Window window = new() { Title = "快速预览" };
            window.Content = imageView;
            imageView.OpenImage(filePath);
            window.Show();
        }
    }


}
