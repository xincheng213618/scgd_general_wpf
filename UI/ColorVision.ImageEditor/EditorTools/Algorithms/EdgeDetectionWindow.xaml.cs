using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
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

        public EdgeDetectionWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            Threshold1Slider.Value = _parameters.LowThreshold;
            Threshold2Slider.Value = _parameters.HighThreshold;
            _ = ApplyPreviewAsync();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || Threshold1Slider == null || Threshold2Slider == null) return;
            _parameters.LowThreshold = Threshold1Slider.Value;
            _parameters.HighThreshold = Threshold2Slider.Value;
            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => _ = ApplyPreviewAsync());
        }

        private async System.Threading.Tasks.Task<bool> ApplyPreviewAsync()
        {
            try
            {
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Canny, _parameters);
                using AlgorithmResult result = await _preview.PreviewAsync(invocation);
                if (result.Status == AlgorithmResultStatus.Failed) log.Warn(string.Join("; ", result.Failures));
                return result.Status == AlgorithmResultStatus.Succeeded && _preview.IsCurrent(invocation.InvocationId);
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
            if (await ApplyPreviewAsync() && _preview.Commit()) Close();
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
