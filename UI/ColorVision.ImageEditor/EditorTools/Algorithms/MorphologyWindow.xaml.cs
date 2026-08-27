using ColorVision.Common.Utilities;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class MorphologyWindow : System.Windows.Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MorphologyWindow));
        private readonly string _debounceKey = $"{nameof(MorphologyWindow)}_{Guid.NewGuid():N}";
        private readonly ImageAlgorithmPreviewSession _preview;

        public MorphologyWindow(ImageProcessingContext image, int defaultOperation = 0)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            OperationCombo.SelectedIndex = defaultOperation;
            _ = ApplyPreviewAsync();
        }

        private void Param_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
            {
                return;
            }

            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => _ = ApplyPreviewAsync());
        }

        private async System.Threading.Tasks.Task<bool> ApplyPreviewAsync()
        {
            try
            {
                int kernelSize = (int)KernelSlider.Value;
                int iterations = Math.Max(1, (int)IterSlider.Value);
                MorphologyParameters parameters = new()
                {
                    Operation = (StandardMorphologyOperation)Math.Max(0, OperationCombo.SelectedIndex),
                    KernelSize = kernelSize % 2 == 0 ? kernelSize + 1 : kernelSize,
                    Iterations = iterations,
                };
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Morphology, parameters);
                using AlgorithmResult result = await _preview.PreviewAsync(invocation);
                return result.Status == AlgorithmResultStatus.Succeeded && _preview.IsCurrent(invocation.InvocationId);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            if (await ApplyPreviewAsync() && _preview.Commit()) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _preview.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            _preview.Dispose();
            base.OnClosed(e);
        }
    }
}
