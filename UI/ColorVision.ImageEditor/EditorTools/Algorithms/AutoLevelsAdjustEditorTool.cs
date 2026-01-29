using ColorVision.Core;
using log4net;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 自动色阶调整工具 - 自动调整图像的色阶
    /// </summary>
    public class AutoLevelsAdjustEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoLevelsAdjustEditorTool));

        private readonly ImageView _imageView;

        public AutoLevelsAdjustEditorTool(ImageView imageView)
        {
            _imageView = imageView;
        }

        public void Execute()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_imageView.HImageCache == null) return;

                log.Info("AutoLevelsAdjust - 开始执行");
                
                int ret = OpenCVMediaHelper.M_AutoLevelsAdjust((HImage)_imageView.HImageCache, out HImage hImageProcessed);
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
                    log.Info("AutoLevelsAdjust 完成");
                }
            });
        }
    }
}
