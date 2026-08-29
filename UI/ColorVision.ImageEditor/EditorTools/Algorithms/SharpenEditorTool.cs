using ColorVision.ImageEditor.Algorithms;
using ColorVision.Algorithms;
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

        public async void Execute()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            log.Info("Sharpen - 开始执行");
            try
            {
                using AlgorithmResult result = await ImageAlgorithmApplier.ApplyAsync(
                    _image,
                    AlgorithmInvocation.Create(StandardAlgorithmIds.Sharpen, new NoAlgorithmParameters()));
                if (result.Status != AlgorithmResultStatus.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Failures));
                log.Info($"Sharpen 完成 - 耗时: {stopwatch.Elapsed}");
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }
        }
    }
}

