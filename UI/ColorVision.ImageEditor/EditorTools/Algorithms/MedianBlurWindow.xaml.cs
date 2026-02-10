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
        private readonly ImageView _imageView;

        public MedianBlurWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;
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
            if (_imageView.HImageCache == null) return;

            int ret = OpenCVMediaHelper.M_ApplyMedianBlur((HImage)_imageView.HImageCache, out HImage hImageProcessed, kernelSize);

            if (ret == 0)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                    {
                        double DpiX = _imageView.Config.GetProperties<double>("DpiX");
                        double DpiY = _imageView.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);
                        hImageProcessed.Dispose();

                        FunctionImage = image;
                    }
                    _imageView.ImageShow.Source = FunctionImage;
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
            FunctionImage = null;
            Close();
        }
    }
}
