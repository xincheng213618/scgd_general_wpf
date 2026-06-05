using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// EdgeDetectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EdgeDetectionWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EdgeDetectionWindow));
        private readonly ImageProcessingContext _image;

        public EdgeDetectionWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _image = image;
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
            if (_image.HImageCache == null) return;

            int ret = OpenCVMediaHelper.M_ApplyCannyEdgeDetection((HImage)_image.HImageCache, out HImage hImageProcessed, threshold1, threshold2);

            if (ret == 0)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
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
                });
            }
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

