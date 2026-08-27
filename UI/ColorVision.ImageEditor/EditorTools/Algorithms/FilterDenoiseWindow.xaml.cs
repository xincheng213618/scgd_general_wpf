using ColorVision.Common.Utilities;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class FilterDenoiseWindow : System.Windows.Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FilterDenoiseWindow));
        private readonly string _debounceKey = $"{nameof(FilterDenoiseWindow)}_{Guid.NewGuid():N}";
        private readonly ImageAlgorithmPreviewSession _preview;

        public FilterDenoiseWindow(ImageProcessingContext image, int defaultFilter = 0)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            FilterCombo.SelectedIndex = defaultFilter;
            UpdatePanelVisibility();
            _ = ApplyPreviewAsync();
        }

        private void Param_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
            {
                return;
            }

            UpdatePanelVisibility();
            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => _ = ApplyPreviewAsync());
        }

        private void UpdatePanelVisibility()
        {
            if (SigmaPanel == null || SigmaColorPanel == null || FilterCombo == null)
            {
                return;
            }

            bool isBilateral = FilterCombo.SelectedIndex == 0;
            SigmaPanel.Visibility = isBilateral ? Visibility.Visible : Visibility.Collapsed;
            SigmaColorPanel.Visibility = isBilateral ? Visibility.Visible : Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task<bool> ApplyPreviewAsync()
        {
            try
            {
                int kernelSize = (int)KernelSlider.Value;
                DenoiseParameters parameters = new()
                {
                    Operation = FilterCombo.SelectedIndex == 0 ? StandardDenoiseOperation.Bilateral : StandardDenoiseOperation.MeanBlur,
                    KernelSize = kernelSize % 2 == 0 ? kernelSize + 1 : kernelSize,
                    SigmaSpace = SigmaSlider.Value,
                    SigmaColor = SigmaColorSlider.Value,
                };
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Denoise, parameters);
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
