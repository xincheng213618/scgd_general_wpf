using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Themes;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class WhiteBalanceWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WhiteBalanceWindow));
        private readonly string _debounceKey = $"{nameof(WhiteBalanceWindow)}_{Guid.NewGuid():N}";
        private readonly ImageAlgorithmPreviewSession _preview;
        private readonly AlgorithmParameterInputs _parameterInputs;
        private readonly WhiteBalanceParameters _parameters = new();
        private long _parameterRevision;
        private Guid _completedPreviewInvocationId;
        private long _completedPreviewParameterRevision;
        private long _completedPreviewInputRevision;

        public WhiteBalanceWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            this.ApplyCaption();
            _preview = ImageAlgorithmPreviewSession.StartForCatalogPreview(image);
            RedSlider.Value = _parameters.RedScale;
            GreenSlider.Value = _parameters.GreenScale;
            BlueSlider.Value = _parameters.BlueScale;
            _parameterInputs = new(this);
            _ = ApplyPreviewAsync();
        }

        private void WhiteBalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || RedSlider == null || GreenSlider == null || BlueSlider == null) return;
            _parameters.RedScale = RedSlider.Value;
            _parameters.GreenScale = GreenSlider.Value;
            _parameters.BlueScale = BlueSlider.Value;
            if (_preview == null) return;
            _parameterRevision++;
            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, _preview.GuardDeferredPreview(() => _ = ApplyPreviewAsync()));
        }

        private async System.Threading.Tasks.Task<bool> ApplyPreviewAsync()
        {
            try
            {
                long parameterRevision = _parameterRevision;
                long inputRevision = _parameterInputs.Revision;
                _completedPreviewInvocationId = Guid.Empty;
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.WhiteBalance, _parameters);
                bool succeeded;
                using (AlgorithmResult result = await _preview.PreviewAsync(invocation))
                {
                    if (result.Status == AlgorithmResultStatus.Failed) log.Warn(string.Join("; ", result.Failures));
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
            bool inputsValid = _parameterInputs.TryCommit();
            DebounceTimer.Cancel(_debounceKey);
            if (!inputsValid) return;
            long parameterRevision = _parameterRevision;
            long inputRevision = _parameterInputs.Revision;
            if (_completedPreviewInvocationId != Guid.Empty
                && parameterRevision == _completedPreviewParameterRevision && inputRevision == _completedPreviewInputRevision
                && _preview.IsCurrent(_completedPreviewInvocationId))
            {
                if (_preview.Commit()) Close();
                // Commit failure can mean a new owner took over during its synchronous callbacks.
                return;
            }
            if (await ApplyPreviewAsync() && parameterRevision == _parameterRevision
                && inputRevision == _parameterInputs.Revision && _preview.Commit()) Close();
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
            _parameterInputs.Dispose();
            _preview.Dispose();
            base.OnClosed(e);
        }
    }
}
