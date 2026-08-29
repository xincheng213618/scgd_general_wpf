using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
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
        private readonly WhiteBalanceParameters _parameters = new();

        public WhiteBalanceWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            RedSlider.Value = _parameters.RedScale;
            GreenSlider.Value = _parameters.GreenScale;
            BlueSlider.Value = _parameters.BlueScale;
            _ = ApplyPreviewAsync();
        }

        private void WhiteBalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || RedSlider == null || GreenSlider == null || BlueSlider == null) return;
            _parameters.RedScale = RedSlider.Value;
            _parameters.GreenScale = GreenSlider.Value;
            _parameters.BlueScale = BlueSlider.Value;
            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => _ = ApplyPreviewAsync());
        }

        private async System.Threading.Tasks.Task<bool> ApplyPreviewAsync()
        {
            try
            {
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.WhiteBalance, _parameters);
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
