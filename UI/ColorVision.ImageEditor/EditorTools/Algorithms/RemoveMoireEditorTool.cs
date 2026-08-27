using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public class RemoveMoireEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RemoveMoireEditorTool));
        private readonly ImageProcessingContext _image;

        public RemoveMoireEditorTool(ImageProcessingContext image) => _image = image;

        public async void Execute()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            log.Info("RemoveMoire - 开始执行");
            try
            {
                using AlgorithmResult result = await ImageAlgorithmApplier.ApplyAsync(
                    _image,
                    AlgorithmInvocation.Create(StandardAlgorithmIds.RemoveMoire, new NoAlgorithmParameters()));
                if (result.Status != AlgorithmResultStatus.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Failures));
                log.Info($"RemoveMoire 完成 - 耗时: {stopwatch.Elapsed}");
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }
        }
    }
}
