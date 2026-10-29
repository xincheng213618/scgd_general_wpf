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
    /// MedianBlurWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MedianBlurWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MedianBlurWindow));
        private readonly string _debounceKey = $"{nameof(MedianBlurWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;
        private long _parameterRevision;
        private Guid _completedPreviewInvocationId;
        private long _completedPreviewParameterRevision;

        public MedianBlurWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            this.ApplyCaption();
            _preview = ImageAlgorithmPreviewSession.StartForCatalogPreview(image);
            _ = ApplyMedianBlurAsync((int)KernelSizeSlider.Value);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            _parameterRevision++;
            if (KernelSizeSlider != null)
            {
                int kernelSize = (int)KernelSizeSlider.Value;
                if (kernelSize % 2 == 0)
                {
                    kernelSize += 1;
                    KernelSizeSlider.Value = kernelSize;
                    return;
                }

                DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, _preview.GuardDeferredPreview(() => _ = ApplyMedianBlurAsync(kernelSize)));
            }
        }

        private async System.Threading.Tasks.Task<bool> ApplyMedianBlurAsync(int kernelSize)
        {
            try
            {
                if (_preview == null) return false;
                long parameterRevision = _parameterRevision;
                _completedPreviewInvocationId = Guid.Empty;
                MedianBlurParameters parameters = new() { KernelSize = kernelSize };
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MedianBlur, parameters);
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
            if (await ApplyMedianBlurAsync(kernelSize) && parameterRevision == _parameterRevision && _preview?.Commit() == true) Close();
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

