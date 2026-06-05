using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// GaussianBlurWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GaussianBlurWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GaussianBlurWindow));
        private readonly string _debounceKey = $"{nameof(GaussianBlurWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;

        public GaussianBlurWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            ApplyGaussianBlur((int)KernelSizeSlider.Value, SigmaSlider.Value);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            if (KernelSizeSlider != null && SigmaSlider != null)
            {
                int kernelSize = (int)KernelSizeSlider.Value;
                if (kernelSize % 2 == 0)
                {
                    kernelSize += 1;
                    KernelSizeSlider.Value = kernelSize;
                    return;
                }

                double sigma = SigmaSlider.Value;
                DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => ApplyGaussianBlur(kernelSize, sigma));
            }
        }

        private void ApplyGaussianBlur(int kernelSize, double sigma)
        {
            try
            {
                _preview?.Apply(mat => OpenCvImageAlgorithms.GaussianBlur(mat, kernelSize, sigma));
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

