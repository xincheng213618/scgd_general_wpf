using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
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
        private readonly ImageProcessingContext _image;

        public BrightnessContrastWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _image = image;
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
            if (_image.HImageCache == null) return;
            
            // 实现类似于PS的效果
            brightness = brightness * 4 / 5;
            contrast = contrast / 300 + 1;
            brightness = _image.HImageCache.Value.depth == 8 ? brightness : brightness * 255;
            
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"AdjustBrightnessContrast - Brightness: {brightness}, Contrast: {contrast}");
            
            int ret = OpenCVMediaHelper.M_AdjustBrightnessContrast((HImage)_image.HImageCache, out HImage hImageProcessed, contrast, brightness);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(_image.FunctionImage, hImageProcessed))
                    {
                        double DpiX = _image.Config.GetProperties<double>("DpiX");
                        double DpiY = _image.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap();
                        
                        hImageProcessed.Dispose();

                        _image.FunctionImage = image;
                    }
                    _image.ImageShow.Source = _image.FunctionImage;
                    stopwatch.Stop();
                    log.Info($"AdjustBrightnessContrast 完成 - 耗时: {stopwatch.Elapsed}");
                }
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // 应用更改到原始图像
            if (_image.FunctionImage is System.Windows.Media.Imaging.WriteableBitmap writeableBitmap)
            {
                _image.ViewBitmapSource = writeableBitmap;
                _image.ImageShow.Source = _image.ViewBitmapSource;
                _image.HImageCache = null;
                _image.FunctionImage = null;
            }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 取消更改，恢复原始图像
            _image.ImageShow.Source = _image.ViewBitmapSource;
            _image.FunctionImage = null;
            Close();
        }
    }
}

