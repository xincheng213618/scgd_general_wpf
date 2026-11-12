using ColorVision.Common.Utilities;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Themes;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// GaussianBlurWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GaussianBlurWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GaussianBlurWindow));
        private readonly string _debounceKey = $"{nameof(GaussianBlurWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;
        private long _parameterRevision;
        private Guid _completedPreviewInvocationId;
        private long _completedPreviewParameterRevision;

        public GaussianBlurWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            this.ApplyCaption();
            _preview = ImageAlgorithmPreviewSession.StartForCatalogPreview(image);
            _ = ApplyGaussianBlurAsync((int)KernelSizeSlider.Value, SigmaSlider.Value);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            _parameterRevision++;
            if (KernelSizeSlider != null && SigmaSlider != null)
            {
                int kernelSize = (int)KernelSizeSlider.Value;
                if (kernelSize % 2 == 0)
                {
                    kernelSize += 1;
                    KernelSizeSlider.Value = kernelSize;
                    return;
                }

                double sigma = SigmaSlider.Value;
                DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, _preview.GuardDeferredPreview(() => _ = ApplyGaussianBlurAsync(kernelSize, sigma)));
            }
        }

        private async System.Threading.Tasks.Task<bool> ApplyGaussianBlurAsync(int kernelSize, double sigma)
        {
            try
            {
                if (_preview == null) return false;
                long parameterRevision = _parameterRevision;
                _completedPreviewInvocationId = Guid.Empty;
                GaussianBlurParameters parameters = new() { KernelSize = kernelSize, Sigma = sigma };
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GaussianBlur, parameters);
                bool succeeded;
                using (AlgorithmResult result = await _preview.PreviewAsync(invocation))
                {
                    succeeded = result.Status == AlgorithmResultStatus.Succeeded;
                }
                // Result disposal can reenter; qualify only the completed run's original parameter state.
                bool current = succeeded && _preview.IsCurrent(invocation.InvocationId);
                if (current && parameterRevision == _parameterRevision)
                {
                    _completedPreviewParameterRevision = parameterRevision;
                    _completedPreviewInvocationId = invocation.InvocationId;
                }
                return current;
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
            long parameterRevision = _parameterRevision;
            if (_completedPreviewInvocationId != Guid.Empty
                && parameterRevision == _completedPreviewParameterRevision && _preview?.IsCurrent(_completedPreviewInvocationId) == true)
            {
                if (_preview?.Commit() == true) Close();
                // Commit failure can mean a new owner took over during its synchronous callbacks.
                return;
            }
            int kernelSize = (int)KernelSizeSlider.Value;
            if (kernelSize % 2 == 0) kernelSize++;
            if (await ApplyGaussianBlurAsync(kernelSize, SigmaSlider.Value) && parameterRevision == _parameterRevision && _preview?.Commit() == true) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _preview?.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            _preview?.Dispose();
            base.OnClosed(e);
        }
    }
}

