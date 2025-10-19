using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class ThresholdWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThresholdWindow));
        private readonly ImageView imageView;

        public ThresholdWindow(ImageView view)
        {
            InitializeComponent();
            imageView = view;
            
            // Set maximum based on image bit depth
            int max = imageView.Config.GetProperties<int>("Max");
            ThresholdSlider.Maximum = max;
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("ThresholdImg", 50, a => ApplyThreshold(), e.NewValue);
        }

        private void ApplyThreshold()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (imageView.HImageCache == null) return;
                
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                double thresh = ThresholdSlider.Value;
                double maxval = imageView.Config.GetProperties<int>("Max");
                int type = 0;
                
                log.Info($"ThresholdImg");
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_Threshold((HImage)imageView.HImageCache, out HImage hImageProcessed, thresh, maxval, type);
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
                            log.Info($"ThresholdImg {stopwatch.Elapsed}");
                        }
                    });
                });
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
