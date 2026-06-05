using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// BrightnessContrastWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BrightnessContrastWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BrightnessContrastWindow));
        private readonly string _debounceKey = $"{nameof(BrightnessContrastWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;

        public BrightnessContrastWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            AdjustBrightnessContrast(ContrastSlider.Value, BrightnessSlider.Value);
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => AdjustBrightnessContrast(ContrastSlider.Value, BrightnessSlider.Value));
        }

        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => AdjustBrightnessContrast(ContrastSlider.Value, BrightnessSlider.Value));
        }

        private void AdjustBrightnessContrast(double contrast, double brightness)
        {
            try
            {
                _preview?.Apply(mat => OpenCvImageAlgorithms.AdjustBrightnessContrast(mat, contrast, brightness));
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _preview?.Commit();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _preview?.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _preview?.CancelIfActive();
            base.OnClosed(e);
        }
    }
}

