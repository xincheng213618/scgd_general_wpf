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
    /// ThresholdWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ThresholdWindow : System.Windows.Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThresholdWindow));
        private readonly ImageProcessingContext _image;
        private readonly string _debounceKey = $"{nameof(ThresholdWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;
        private readonly AlgorithmParameterInputs _parameterInputs;
        private long _parameterRevision;
        private Guid _completedPreviewInvocationId;
        private long _completedPreviewParameterRevision;
        private long _completedPreviewInputRevision;

        public ThresholdWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            this.ApplyCaption();
            _image = image;
            _preview = ImageAlgorithmPreviewSession.StartForCatalogPreview(image);
            
            ThresholdParameters defaults = new();
            ThresholdSlider.Maximum = byte.MaxValue;
            ThresholdSlider.Value = defaults.Threshold;
            _parameterInputs = new(this);
            _ = ApplyThresholdAsync();
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            _parameterRevision++;
            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, _preview.GuardDeferredPreview(() => _ = ApplyThresholdAsync()));
        }

        private async System.Threading.Tasks.Task<bool> ApplyThresholdAsync()
        {
            if (_preview == null)
            {
                return false;
            }

            try
            {
                long parameterRevision = _parameterRevision;
                long inputRevision = _parameterInputs.Revision;
                _completedPreviewInvocationId = Guid.Empty;
                ThresholdParameters parameters = CreateParameters(ThresholdSlider.Value);
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Threshold, parameters);
                bool succeeded;
                using (AlgorithmResult result = await _preview.PreviewAsync(invocation))
                {
                    succeeded = result.Status == AlgorithmResultStatus.Succeeded;
                }
                // Result disposal can reenter; qualify only the completed run's original parameter state.
                bool current = succeeded && _preview.IsCurrent(invocation.InvocationId);
                if (current && parameterRevision == _parameterRevision && inputRevision == _parameterInputs.Revision)
                {
                    _completedPreviewParameterRevision = parameterRevision;
                    _completedPreviewInputRevision = inputRevision;
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

        internal static ThresholdParameters CreateParameters(double threshold)
            => new() { Threshold = threshold, UseNominalRange = true };

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            bool inputsValid = _parameterInputs.TryCommit();
            DebounceTimer.Cancel(_debounceKey);
            if (!inputsValid) return;
            long parameterRevision = _parameterRevision;
            long inputRevision = _parameterInputs.Revision;
            if (_completedPreviewInvocationId != Guid.Empty
                && parameterRevision == _completedPreviewParameterRevision && inputRevision == _completedPreviewInputRevision
                && _preview?.IsCurrent(_completedPreviewInvocationId) == true)
            {
                if (_preview?.Commit() == true) Close();
                // Commit failure can mean a new owner took over during its synchronous callbacks.
                return;
            }
            if (await ApplyThresholdAsync() && parameterRevision == _parameterRevision
                && inputRevision == _parameterInputs.Revision && _preview?.Commit() == true) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _preview?.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            _parameterInputs.Dispose();
            _preview?.Dispose();
            base.OnClosed(e);
        }
    }
}

