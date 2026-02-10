using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// GaussianBlurWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GaussianBlurWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GaussianBlurWindow));
        private readonly ImageView _imageView;

        public GaussianBlurWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (KernelSizeSlider != null && SigmaSlider != null)
            {
                // Ensure kernel size is odd
                int kernelSize = (int)KernelSizeSlider.Value;
                if (kernelSize % 2 == 0)
                {
                    kernelSize += 1;
                    KernelSizeSlider.Value = kernelSize;
                    return;
                }

                double sigma = SigmaSlider.Value;
                DebounceTimer.AddOrResetTimer("ApplyGaussianBlur", 50, () => ApplyGaussianBlur(kernelSize, sigma));
            }
        }

        private void ApplyGaussianBlur(int kernelSize, double sigma)
        {
            if (_imageView.HImageCache == null) return;

            int ret = OpenCVMediaHelper.M_ApplyGaussianBlur((HImage)_imageView.HImageCache, out HImage hImageProcessed, kernelSize, sigma);

            if (ret == 0)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
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
                _imageView.HImageCache = null;
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
