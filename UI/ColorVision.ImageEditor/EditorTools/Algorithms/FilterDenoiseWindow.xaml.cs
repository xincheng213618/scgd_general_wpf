using ColorVision.Common.Utilities;
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
            ApplyPreview();
        }

        private void Param_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
            {
                return;
            }

            UpdatePanelVisibility();
            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, ApplyPreview);
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

        private void ApplyPreview()
        {
            try
            {
                int kernelSize = (int)KernelSlider.Value;
                FilterDenoiseOperation operation = FilterCombo.SelectedIndex == 0
                    ? FilterDenoiseOperation.Bilateral
                    : FilterDenoiseOperation.Blur;
                double sigmaSpace = SigmaSlider.Value;
                double sigmaColor = SigmaColorSlider.Value;

                _preview.Apply(mat => OpenCvImageAlgorithms.FilterDenoise(mat, operation, kernelSize, sigmaColor, sigmaSpace));
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _preview.Commit();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _preview.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _preview.CancelIfActive();
            base.OnClosed(e);
        }
    }
}
