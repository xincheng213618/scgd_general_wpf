using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// MedianBlurWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MedianBlurWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MedianBlurWindow));
        private readonly ImageProcessingContext _image;

        public MedianBlurWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _image = image;
        }
        public ImageSource FunctionImage { get; set; }


        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (KernelSizeSlider != null)
            {
                // Ensure kernel size is odd
                int kernelSize = (int)KernelSizeSlider.Value;
                if (kernelSize % 2 == 0)
                {
                    kernelSize += 1;
                    KernelSizeSlider.Value = kernelSize;
                    return;
                }

                DebounceTimer.AddOrResetTimer("ApplyMedianBlur", 50, () => ApplyMedianBlur(kernelSize));
            }
        }

        private void ApplyMedianBlur(int kernelSize)
        {
            if (_image.HImageCache == null) return;

            int ret = OpenCVMediaHelper.M_ApplyMedianBlur((HImage)_image.HImageCache, out HImage hImageProcessed, kernelSize);

            if (ret == 0)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                    {
                        double DpiX = _image.Config.GetProperties<double>("DpiX");
                        double DpiY = _image.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);
                        hImageProcessed.Dispose();

                        FunctionImage = image;
                    }
                    _image.ImageShow.Source = FunctionImage;
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
            FunctionImage = null;
            Close();
        }
    }
}

