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
            ImageFrameLease? lease = _image.AcquireImageFrame();
            if (lease == null) return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            long revision = lease.Revision;
            log.Info("RemoveMoire - 开始执行");

            _ = Task.Run(() =>
            {
                int ret;
                HImage hImageProcessed;
                using (lease)
                {
                    ret = OpenCVMediaHelper.M_RemoveMoire(lease.Image, out hImageProcessed);
                }

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (ret != 0 || !_image.IsCurrentImageRevision(revision))
                    {
                        hImageProcessed.Dispose();
                        return;
                    }

                    if (!HImageExtension.UpdateWriteableBitmap(_image.ViewBitmapSource, hImageProcessed))
                    {
                        _image.ViewBitmapSource = hImageProcessed.ToWriteableBitmapAndDispose();
                    }

                    _image.NotifySourcePixelsChanged();
                    _image.ImageShow.Source = _image.ViewBitmapSource;
                    stopwatch.Stop();
                    log.Info($"RemoveMoire 完成 - 耗时: {stopwatch.Elapsed}");
                });
            });
        }
    }
}

