using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// BrightnessContrastWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BrightnessContrastWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BrightnessContrastWindow));
        private readonly ImageView _imageView;

        public BrightnessContrastWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;
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
            if (_imageView.HImageCache == null) return;
            
            // 实现类似于PS的效果
            brightness = brightness * 4 / 5;
            contrast = contrast / 300 + 1;
            brightness = _imageView.HImageCache.Value.depth == 8 ? brightness : brightness * 255;
            
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"AdjustBrightnessContrast - Brightness: {brightness}, Contrast: {contrast}");
            
            int ret = OpenCVMediaHelper.M_AdjustBrightnessContrast((HImage)_imageView.HImageCache, out HImage hImageProcessed, contrast, brightness);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(_imageView.FunctionImage, hImageProcessed))
                    {
                        double DpiX = _imageView.Config.GetProperties<double>("DpiX");
                        double DpiY = _imageView.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap();
                        
                        hImageProcessed.Dispose();

                        _imageView.FunctionImage = image;
                    }
                    _imageView.ImageShow.Source = _imageView.FunctionImage;
                    stopwatch.Stop();
                    log.Info($"AdjustBrightnessContrast 完成 - 耗时: {stopwatch.Elapsed}");
                }
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // 应用更改到原始图像
            if (_imageView.FunctionImage is System.Windows.Media.Imaging.WriteableBitmap writeableBitmap)
            {
                _imageView.ViewBitmapSource = writeableBitmap;
                _imageView.ImageShow.Source = _imageView.ViewBitmapSource;
                _imageView.HImageCache = writeableBitmap.ToHImage();
                _imageView.FunctionImage = null;
            }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 取消更改，恢复原始图像
            _imageView.ImageShow.Source = _imageView.ViewBitmapSource;
            _imageView.FunctionImage = null;
            Close();
        }
    }
}
