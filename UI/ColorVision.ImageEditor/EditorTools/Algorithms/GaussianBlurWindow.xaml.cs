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
        private readonly ImageProcessingContext _image;

        public GaussianBlurWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _image = image;
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
            if (_image.HImageCache == null) return;

            int ret = OpenCVMediaHelper.M_ApplyGaussianBlur((HImage)_image.HImageCache, out HImage hImageProcessed, kernelSize, sigma);

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

