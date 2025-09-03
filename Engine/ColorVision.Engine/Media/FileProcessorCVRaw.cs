using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Shell;
using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Impl.SolutionImpl
{
    [FileExtension(".cvraw|.cvcie")]
    public class FileProcessorCVRaw : IFileProcessor
    {
        public int Order => 1;

        public bool CanProcess(string filePath)
        {
            return true;
        }
        public void Export(string filePath)
        {
            var parser = ArgumentParser.GetInstance();
            parser.AddArgument("quiet", true, "q");
            parser.AddArgument("mx", false, "mx");
            parser.AddArgument("type", false, "t");
            parser.AddArgument("output", false, "o");

            parser.Parse();

            var vie = new VExportCIE(filePath);

            string mxs = parser.GetValue("mx");
            if (int.TryParse(mxs, out int mx))
            {
                vie.Compression = mx;
            }

            string type = parser.GetValue("type");
            if (type != null)
            {
                vie.ExportImageFormat = type switch
                {
                    "tif" => ImageFormat.Tiff,
                    "png" => ImageFormat.Png,
                    "jpg" => ImageFormat.Jpeg,
                    _ => ImageFormat.Tiff,
                };
            }
            string output = parser.GetValue("output");
            if (output != null && Directory.Exists(output))
            {
                vie.SavePath = output;
            }
            else
            {
                vie.SavePath = Directory.GetParent(filePath)?.FullName ?? string.Empty;
            }

            if (parser.GetFlag("quiet"))
            {
                VExportCIE.SaveToTif(vie);
                Environment.Exit(0);
                return;
            }
            new ExportCVCIE(vie).Show();
        }


        public bool CanExport(string filePath)
        {
            return true;
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
