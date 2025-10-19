using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 去除摩尔纹工具 - 去除图像中的摩尔纹
    /// </summary>
    public class RemoveMoireEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RemoveMoireEditorTool));

        private readonly ImageView _imageView;

        public RemoveMoireEditorTool(ImageView imageView)
        {
            _imageView = imageView;
        }

        public void Execute()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_imageView.HImageCache == null) return;
                
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info("RemoveMoire - 开始执行");
                
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_RemoveMoire((HImage)_imageView.HImageCache, out HImage hImageProcessed);
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
                            log.Info($"RemoveMoire 完成 - 耗时: {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }
    }
}
