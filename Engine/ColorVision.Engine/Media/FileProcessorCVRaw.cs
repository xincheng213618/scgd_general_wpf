﻿using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.Themes;
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

        public void Process(string filePath)
        {
            ImageView imageView = new();
            Window window = new Window() { Title = filePath };
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

            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ImageViewModel.ClearImage();
                }));
            }
            window.ApplyCaption();
            window.Show();
        }
    }


}
