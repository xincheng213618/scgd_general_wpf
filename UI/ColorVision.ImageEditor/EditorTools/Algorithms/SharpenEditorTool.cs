using ColorVision.Core;
using log4net;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 图像锐化工具 - 增强图像的边缘和细节
    /// </summary>
    public class SharpenEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SharpenEditorTool));

        private readonly ImageProcessingContext _image;

        public SharpenEditorTool(ImageProcessingContext image)
        {
            _image = image;
        }

        public void Execute()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_image.HImageCache == null) return;
                
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info("Sharpen - 开始执行");
                
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_ApplySharpen((HImage)_image.HImageCache, out HImage hImageProcessed);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (ret == 0)
                        {
                            if (!HImageExtension.UpdateWriteableBitmap(_image.ViewBitmapSource, hImageProcessed))
                            {
                                double DpiX = _image.Config.GetProperties<double>("DpiX");
                                double DpiY = _image.Config.GetProperties<double>("DpiY");
                                var image = hImageProcessed.ToWriteableBitmap();
                                hImageProcessed.Dispose();
                                _image.ViewBitmapSource = image;
                            }
                            _image.HImageCache?.Dispose();
                            _image.HImageCache = null;
                            _image.ImageShow.Source = _image.ViewBitmapSource;
                            stopwatch.Stop();
                            log.Info($"Sharpen 完成 - 耗时: {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }
    }
}

