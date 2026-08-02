using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Themes;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// BasicAdjustmentWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BasicAdjustmentWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BasicAdjustmentWindow));
        private readonly string _debounceKey = $"{nameof(BasicAdjustmentWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;
        private bool _isResetting;

        public BasicAdjustmentWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            this.ApplyCaption();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            ApplyAdjustment();
        }

        private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null || _isResetting)
            {
                return;
            }

            if (PreviewCheckBox.IsChecked == true)
            {
                DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, ApplyAdjustment);
            }
        }

        private void ApplyAdjustment()
        {
            try
            {
                _preview?.Apply(mat => OpenCvImageAlgorithms.AdjustBasic(
                    mat,
                    ExposureSlider.Value,
                    BrightnessSlider.Value,
                    ContrastSlider.Value,
                    GammaSlider.Value));
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(this, ex.Message);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _isResetting = true;
            try
            {
                ExposureSlider.Value = 0;
                BrightnessSlider.Value = 0;
                ContrastSlider.Value = 0;
                GammaSlider.Value = 1;
            }
            finally
            {
                _isResetting = false;
            }

            DebounceTimer.Cancel(_debounceKey);
            if (PreviewCheckBox.IsChecked == true)
            {
                ApplyAdjustment();
            }
            else
            {
                _preview?.ShowOriginal();
            }
        }

        private void PreviewCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            DebounceTimer.Cancel(_debounceKey);
            if (PreviewCheckBox.IsChecked == true)
            {
                ApplyAdjustment();
            }
            else
            {
                _preview.ShowOriginal();
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            ApplyAdjustment();
            _preview?.Commit();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            _preview?.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            _preview?.CancelIfActive();
            base.OnClosed(e);
        }
    }
}

