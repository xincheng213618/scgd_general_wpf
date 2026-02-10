using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// WhiteBalanceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WhiteBalanceWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WhiteBalanceWindow));
        private readonly ImageView _imageView;

        public WhiteBalanceWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;

            // 从配置中加载当前白平衡值
            RedSlider.Value = 1;
            GreenSlider.Value = 1;
            BlueSlider.Value = 1;
        }

        private void WhiteBalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RedSlider != null && GreenSlider !=null && BlueSlider !=null)
            {
                double red = RedSlider.Value;
                double green = GreenSlider.Value;
                double blue = BlueSlider.Value;
                DebounceTimer.AddOrResetTimer("AdjustWhiteBalance", 30, () => AdjustWhiteBalance(red, green, blue));
            }

        }

        private void AdjustWhiteBalance(double red,double green, double blue)
        {
            if (_imageView.HImageCache == null) return;

            PixelFormat pixelFormat = _imageView.Config.GetProperties<PixelFormat>("PixelFormat");
            
            int ret;
            ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)_imageView.HImageCache, out HImage hImageProcessed, red, green, blue);

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
