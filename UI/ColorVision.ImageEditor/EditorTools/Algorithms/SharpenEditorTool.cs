using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Diagnostics;
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
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info("Sharpen - 开始执行");

                try
                {
                    ImageAlgorithmApplier.Apply(_image, OpenCvImageAlgorithms.Sharpen);
                    stopwatch.Stop();
                    log.Info($"Sharpen 完成 - 耗时: {stopwatch.Elapsed}");
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

