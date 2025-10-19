using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// EdgeDetectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EdgeDetectionWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EdgeDetectionWindow));
        private readonly ImageView _imageView;

        public EdgeDetectionWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Threshold1Slider != null && Threshold2Slider != null)
            {
                double threshold1 = Threshold1Slider.Value;
                double threshold2 = Threshold2Slider.Value;
                DebounceTimer.AddOrResetTimer("ApplyEdgeDetection", 50, () => ApplyEdgeDetection(threshold1, threshold2));
            }
        }

        private void ApplyEdgeDetection(double threshold1, double threshold2)
        {
            if (_imageView.HImageCache == null) return;

            int ret = OpenCVMediaHelper.M_ApplyCannyEdgeDetection((HImage)_imageView.HImageCache, out HImage hImageProcessed, threshold1, threshold2);

            if (ret == 0)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (!HImageExtension.UpdateWriteableBitmap(_imageView.FunctionImage, hImageProcessed))
                    {
                        double DpiX = _imageView.Config.GetProperties<double>("DpiX");
                        double DpiY = _imageView.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);
                        hImageProcessed.Dispose();

                        _imageView.FunctionImage = image;
                    }
                    _imageView.ImageShow.Source = _imageView.FunctionImage;
                });
            }
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
