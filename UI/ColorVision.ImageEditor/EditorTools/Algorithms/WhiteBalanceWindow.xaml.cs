#pragma warning disable CS8625
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
        private readonly ImageProcessingContext _image;

        public WhiteBalanceWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _image = image;

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
            using ImageFrameLease? lease = _image.AcquireImageFrame();
            if (lease == null) return;

            long revision = lease.Revision;
            int ret = OpenCVMediaHelper.M_GetWhiteBalance(lease.Image, out HImage hImageProcessed, red, green, blue);

            if (ret != 0)
            {
                hImageProcessed.Dispose();
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!_image.IsCurrentImageRevision(revision))
                {
                    hImageProcessed.Dispose();
                    return;
                }

                if (!HImageExtension.UpdateWriteableBitmap(_image.FunctionImage, hImageProcessed))
                    _image.FunctionImage = hImageProcessed.ToWriteableBitmapAndDispose();

                _image.ImageShow.Source = _image.FunctionImage;
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // 应用更改到原始图像
            if (_image.FunctionImage is System.Windows.Media.Imaging.WriteableBitmap writeableBitmap)
            {
                _image.ViewBitmapSource = writeableBitmap;
                _image.ImageShow.Source = _image.ViewBitmapSource;
                _image.NotifySourcePixelsChanged();
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

