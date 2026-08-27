using ColorVision.ImageEditor.Algorithms;
using ColorVision.Algorithms;
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

        public async void Execute()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            log.Info("InvertImage - 开始执行");
            try
            {
                using AlgorithmResult result = await ImageAlgorithmApplier.ApplyAsync(
                    _image,
                    AlgorithmInvocation.Create(StandardAlgorithmIds.Invert, new NoAlgorithmParameters()));
                EnsureSucceeded(result);
                log.Info($"InvertImage 完成 - 耗时: {stopwatch.Elapsed}");
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }
        }

        private static void EnsureSucceeded(AlgorithmResult result)
        {
            if (result.Status == AlgorithmResultStatus.Succeeded) return;
            throw new InvalidOperationException(string.Join("; ", result.Failures));
        }
    }
}

