using ColorVision.Core;
using log4net;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class WhiteBalanceWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WhiteBalanceWindow));
        private readonly ImageView imageView;

        public WhiteBalanceWindow(ImageView view)
        {
            InitializeComponent();
            imageView = view;
            
            // Initialize slider values from config
            RedSlider.Value = imageView.Config.RedBalance;
            GreenSlider.Value = imageView.Config.GreenBalance;
            BlueSlider.Value = imageView.Config.BlueBalance;
        }

        private void BalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (imageView.HImageCache == null) return;
            
            PixelFormat pixelFormat = imageView.Config.GetProperties<PixelFormat>("PixelFormat");
            if (pixelFormat == PixelFormats.Rgb48)
            {
                // 算法本身有余数，这里优化一下
                int ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)imageView.HImageCache, out HImage hImageProcessed, 
                    BlueSlider.Value, GreenSlider.Value, RedSlider.Value);
                if (ret == 0)
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        if (!HImageExtension.UpdateWriteableBitmap(imageView.FunctionImage, hImageProcessed))
                        {
                            double DpiX = imageView.Config.GetProperties<double>("DpiX");
                            double DpiY = imageView.Config.GetProperties<double>("DpiY");
                            var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);
                            hImageProcessed.Dispose();

                            imageView.FunctionImage = image;
                        }
                        imageView.ImageShow.Source = imageView.FunctionImage;
                    });
                }
            }
            else
            {
                int ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)imageView.HImageCache, out HImage hImageProcessed, 
                    RedSlider.Value, GreenSlider.Value, BlueSlider.Value);
                if (ret == 0)
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        if (!HImageExtension.UpdateWriteableBitmap(imageView.FunctionImage, hImageProcessed))
                        {
                            double DpiX = imageView.Config.GetProperties<double>("DpiX");
                            double DpiY = imageView.Config.GetProperties<double>("DpiY");
                            var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);
                            hImageProcessed.Dispose();

                            imageView.FunctionImage = image;
                        }
                        imageView.ImageShow.Source = imageView.FunctionImage;
                    });
                }
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            RedSlider.Value = 1;
            GreenSlider.Value = 1;
            BlueSlider.Value = 1;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (imageView.FunctionImage is System.Windows.Media.Imaging.WriteableBitmap writeableBitmap)
            {
                imageView.ViewBitmapSource = writeableBitmap;
                imageView.ImageShow.Source = imageView.ViewBitmapSource;
                imageView.HImageCache = writeableBitmap.ToHImage();
                imageView.FunctionImage = null;
                imageView.Config.RedBalance = 1;
                imageView.Config.GreenBalance = 1;
                imageView.Config.BlueBalance = 1;
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            imageView.ImageShow.Source = imageView.ViewBitmapSource;
            imageView.FunctionImage = null;
            DialogResult = false;
            Close();
        }
    }
}
