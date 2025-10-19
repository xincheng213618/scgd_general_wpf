using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
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
            RedSlider.Value = _imageView.Config.RedBalance;
            GreenSlider.Value = _imageView.Config.GreenBalance;
            BlueSlider.Value = _imageView.Config.BlueBalance;
        }

        private void WhiteBalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustWhiteBalance", 30, AdjustWhiteBalance);
        }

        private void AdjustWhiteBalance()
        {
            if (_imageView.HImageCache == null) return;

            PixelFormat pixelFormat = _imageView.Config.GetProperties<PixelFormat>("PixelFormat");
            
            int ret;
            if (pixelFormat == PixelFormats.Rgb48)
            {
                // 算法本身有余数，这里优化一下
                ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)_imageView.HImageCache, out HImage hImageProcessed, BlueSlider.Value, GreenSlider.Value, RedSlider.Value);
            }
            else
            {
                ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)_imageView.HImageCache, out HImage hImageProcessed, RedSlider.Value, GreenSlider.Value, BlueSlider.Value);
            }
            
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
                
                // 更新配置中的白平衡值
                _imageView.Config.RedBalance = RedSlider.Value;
                _imageView.Config.GreenBalance = GreenSlider.Value;
                _imageView.Config.BlueBalance = BlueSlider.Value;
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
