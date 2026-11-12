using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Themes;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class EdgeDetectionWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EdgeDetectionWindow));
        private readonly string _debounceKey = $"{nameof(EdgeDetectionWindow)}_{Guid.NewGuid():N}";
        private readonly ImageAlgorithmPreviewSession _preview;
        private readonly CannyParameters _parameters = new();
        private long _parameterRevision;
        private Guid _completedPreviewInvocationId;
        private long _completedPreviewParameterRevision;

        public EdgeDetectionWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            this.ApplyCaption();
            _preview = ImageAlgorithmPreviewSession.StartForCatalogPreview(image);
            Threshold1Slider.Value = _parameters.LowThreshold;
            Threshold2Slider.Value = _parameters.HighThreshold;
            _ = ApplyPreviewAsync();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || Threshold1Slider == null || Threshold2Slider == null) return;
            _parameters.LowThreshold = Threshold1Slider.Value;
            _parameters.HighThreshold = Threshold2Slider.Value;
            if (_preview == null) return;
            _parameterRevision++;
            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, _preview.GuardDeferredPreview(() => _ = ApplyPreviewAsync()));
        }

        private async System.Threading.Tasks.Task<bool> ApplyPreviewAsync()
        {
            try
            {
                long parameterRevision = _parameterRevision;
                _completedPreviewInvocationId = Guid.Empty;
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Canny, _parameters);
                bool succeeded;
                using (AlgorithmResult result = await _preview.PreviewAsync(invocation))
                {
                    if (result.Status == AlgorithmResultStatus.Failed) log.Warn(string.Join("; ", result.Failures));
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
            catch (ObjectDisposedException)
            {
                return false;
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
                && parameterRevision == _completedPreviewParameterRevision && _preview.IsCurrent(_completedPreviewInvocationId))
            {
                if (_preview.Commit()) Close();
                // Commit failure can mean a new owner took over during its synchronous callbacks.
                return;
            }
            if (await ApplyPreviewAsync() && parameterRevision == _parameterRevision && _preview.Commit()) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
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
