using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class BrightnessContrastWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BrightnessContrastWindow));
        private readonly ImageView imageView;

        public BrightnessContrastWindow(ImageView view)
        {
            InitializeComponent();
            imageView = view;
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, AdjustBrightnessContrast, ContrastSlider.Value, BrightnessSlider.Value);
        }

        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, AdjustBrightnessContrast, ContrastSlider.Value, BrightnessSlider.Value);
        }

        private void AdjustBrightnessContrast(double contrast, double brightness)
        {
            if (imageView.HImageCache == null) return;
            
            // 实现类似于PS的效果
            brightness = brightness * 4 / 5;
            contrast = contrast / 300 + 1;
            brightness = imageView.HImageCache.Value.depth == 8 ? brightness : brightness * 255;
            
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ImagePath，正在执行AdjustBrightnessContrast,Brightness{brightness},Contrast{contrast}");
            
            int ret = OpenCVMediaHelper.M_AdjustBrightnessContrast((HImage)imageView.HImageCache, out HImage hImageProcessed, contrast, brightness);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(imageView.FunctionImage, hImageProcessed))
                    {
                        double DpiX = imageView.Config.GetProperties<double>("DpiX");
                        double DpiY = imageView.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);
                        
                        hImageProcessed.Dispose();

                        imageView.FunctionImage = image;
                    }
                    imageView.ImageShow.Source = imageView.FunctionImage;
                    stopwatch.Stop();
                    log.Info($"AdjustBrightnessContrast {stopwatch.Elapsed}");
                }
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (imageView.FunctionImage is System.Windows.Media.Imaging.WriteableBitmap writeableBitmap)
            {
                imageView.ViewBitmapSource = writeableBitmap;
                imageView.ImageShow.Source = imageView.ViewBitmapSource;
                imageView.HImageCache = writeableBitmap.ToHImage();
                imageView.FunctionImage = null;
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            imageView.ImageShow.Source = imageView.ViewBitmapSource;
            imageView.FunctionImage = null;
            DialogResult = false;
            Close();
        }
    }
}
