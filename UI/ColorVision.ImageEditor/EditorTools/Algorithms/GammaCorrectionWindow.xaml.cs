using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class GammaCorrectionWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GammaCorrectionWindow));
        private readonly ImageView imageView;
        private double originalGamma;

        public GammaCorrectionWindow(ImageView view)
        {
            InitializeComponent();
            imageView = view;
            originalGamma = 1.0;
        }

        private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("ApplyGammaCorrection", 50, a => ApplyGammaCorrection(a), GammaSlider.Value);
        }

        private void ApplyGammaCorrection(double gamma)
        {
            if (imageView.HImageCache == null) return;
            
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ImagePath，正在执行ApplyGammaCorrection,Gamma{gamma}");
            
            int ret = OpenCVMediaHelper.M_ApplyGammaCorrection((HImage)imageView.HImageCache, out HImage hImageProcessed, gamma);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ret == 0)
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
                    stopwatch.Stop();
                    log.Info($"ApplyGammaCorrection {stopwatch.Elapsed}");
                }
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (imageView.FunctionImage is System.Windows.Media.Imaging.WriteableBitmap writeableBitmap)
            {
                imageView.ViewBitmapSource = writeableBitmap;
                imageView.ImageShow.Source = imageView.ViewBitmapSource;
                imageView.HImageCache = writeableBitmap.ToHImage();
                imageView.FunctionImage = null;
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
