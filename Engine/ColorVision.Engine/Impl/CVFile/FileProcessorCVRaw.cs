using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Shell;
using System;
using System.IO;
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
            var parser = ArgumentParser.GetInstance();
            parser.AddArgument("quiet", true, "q");
            parser.Parse();

            if (parser.GetFlag("quiet"))
            {
                var vie = new VExportCIE(filePath);
                vie.SavePath = Directory.GetParent(filePath)?.FullName ?? string.Empty;
                VExportCIE.SaveToTif(vie);
                Environment.Exit(0);
                return;
            }

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
