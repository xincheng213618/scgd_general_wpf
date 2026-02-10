using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// GammaCorrectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GammaCorrectionWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GammaCorrectionWindow));
        private readonly ImageView _imageView;
        HImage hImage;
        public GammaCorrectionWindow(ImageView imageView)
        {
            hImage = imageView.HImageCache ?? new HImage();
            InitializeComponent();
            _imageView = imageView;
        }

        private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            double gammaValue = e.NewValue;
            TaskConflator.RunOrUpdate("ApplyGammaCorrection", ()=> ApplyGammaCorrection(gammaValue) );
        }

        private void ApplyGammaCorrection(double gamma)
        {            
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ApplyGammaCorrection - Gamma: {gamma}");
            
            int ret = OpenCVMediaHelper.M_ApplyGammaCorrection(hImage, out HImage hImageProcessed, gamma);
            double algoMs = stopwatch.Elapsed.TotalMilliseconds;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ret == 0)
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
                    double renderMs = stopwatch.Elapsed.TotalMilliseconds;
                    stopwatch.Stop();
                    string perfMsg = $"算法耗时: {algoMs:F2} ms | 渲染耗时: {renderMs - algoMs:F2} ms | 总计: {(renderMs):F2} ms";
                    log.Info(perfMsg);
                }
            });
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
