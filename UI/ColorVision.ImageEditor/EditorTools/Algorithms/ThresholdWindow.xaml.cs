using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// ThresholdWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ThresholdWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThresholdWindow));
        private readonly ImageView _imageView;

        public ThresholdWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;
            
            // 根据图像深度设置最大值
            int maxVal = _imageView.Config.GetProperties<int>("Max");
            ThresholdSlider.Maximum = maxVal;
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("Threshold", 50, a => ApplyThreshold(), ThresholdSlider.Value);
        }

        private void ApplyThreshold()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_imageView.HImageCache == null) return;
                
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                double thresh = ThresholdSlider.Value;
                double maxval = _imageView.Config.GetProperties<int>("Max");
                int type = 0;
                
                log.Info($"Threshold - 阈值: {thresh}");
                
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_Threshold((HImage)_imageView.HImageCache, out HImage hImageProcessed, thresh, maxval, type);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (ret == 0)
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
                            stopwatch.Stop();
                            log.Info($"Threshold 完成 - 耗时: {stopwatch.Elapsed}");
                        }
                    });
                });
            });
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
            _imageView.FunctionImage = null;
            Close();
        }
    }
}
