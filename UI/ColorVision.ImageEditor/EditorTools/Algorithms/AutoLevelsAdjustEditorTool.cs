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

        private readonly ImageProcessingContext _image;

        public AutoLevelsAdjustEditorTool(ImageProcessingContext image)
        {
            _image = image;
        }

        public void Execute()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_image.HImageCache == null) return;

                log.Info("AutoLevelsAdjust - 开始执行");
                
                int ret = OpenCVMediaHelper.M_AutoLevelsAdjust((HImage)_image.HImageCache, out HImage hImageProcessed);
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(_image.FunctionImage, hImageProcessed))
                    {
                        double DpiX = _image.Config.GetProperties<double>("DpiX");
                        double DpiY = _image.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap();

                        hImageProcessed.Dispose();
                        _image.FunctionImage = image;
                    }
                    _image.ImageShow.Source = _image.FunctionImage;
                    log.Info("AutoLevelsAdjust 完成");
                }
            });
        }
    }
}

