using ColorVision.Common.MVVM;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 图像反相工具 - 反转图像的颜色
    /// </summary>
    public class InvertEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(InvertEditorTool));

        private readonly ImageView _imageView;

        public InvertEditorTool(ImageView imageView)
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
                log.Info("InvertImage - 开始执行");
                
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_InvertImage((HImage)_imageView.HImageCache, out HImage hImageProcessed);
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
                            log.Info($"InvertImage 完成 - 耗时: {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }
    }
}
