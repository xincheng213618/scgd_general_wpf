using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// GammaCorrectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GammaCorrectionWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GammaCorrectionWindow));
        private readonly ImageView _imageView;

        public GammaCorrectionWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;
        }

        private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("ApplyGammaCorrection", 50, a => ApplyGammaCorrection(a), GammaSlider.Value);
        }

        private void ApplyGammaCorrection(double gamma)
        {
            if (_imageView.HImageCache == null) return;
            
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ApplyGammaCorrection - Gamma: {gamma}");
            
            int ret = OpenCVMediaHelper.M_ApplyGammaCorrection((HImage)_imageView.HImageCache, out HImage hImageProcessed, gamma);
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
                    log.Info($"ApplyGammaCorrection 完成 - 耗时: {stopwatch.Elapsed}");
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
