using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 图像反相工具 - 反转图像的颜色
    /// </summary>
    public class InvertEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(InvertEditorTool));

        private readonly ImageProcessingContext _image;

        public InvertEditorTool(ImageProcessingContext image)
        {
            _image = image;
        }

        public void Execute()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info("InvertImage - 开始执行");

                try
                {
                    ImageAlgorithmApplier.Apply(_image, OpenCvImageAlgorithms.Invert);
                    stopwatch.Stop();
                    log.Info($"InvertImage 完成 - 耗时: {stopwatch.Elapsed}");
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    MessageBox.Show(ex.Message);
                }
            });
        }
    }
}

