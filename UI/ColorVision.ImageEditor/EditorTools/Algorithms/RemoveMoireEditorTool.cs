using ColorVision.Core;
using log4net;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 去除摩尔纹工具 - 去除图像中的摩尔纹干扰
    /// </summary>
    public class RemoveMoireEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RemoveMoireEditorTool));

        private readonly ImageProcessingContext _image;

        public RemoveMoireEditorTool(ImageProcessingContext image)
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
                log.Info("RemoveMoire - 开始执行");
                
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_RemoveMoire((HImage)_image.HImageCache, out HImage hImageProcessed);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (ret == 0)
                        {
                            if (!HImageExtension.UpdateWriteableBitmap(_image.ViewBitmapSource, hImageProcessed))
                            {
                                double DpiX = _image.Config.GetProperties<double>("DpiX");
                                double DpiY = _image.Config.GetProperties<double>("DpiY");
                                var image = hImageProcessed.ToWriteableBitmapAndDispose();
                                _image.ViewBitmapSource = image;
                            }
                            _image.HImageCache?.Dispose();
                            _image.HImageCache = null;
                            _image.ImageShow.Source = _image.ViewBitmapSource;
                            stopwatch.Stop();
                            log.Info($"RemoveMoire 完成 - 耗时: {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }
    }
}

