using ColorVision.Common.Utilities;
using ColorVision.Algorithms;
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
            _ = ApplyGaussianBlurAsync((int)KernelSizeSlider.Value, SigmaSlider.Value);
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
                DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => _ = ApplyGaussianBlurAsync(kernelSize, sigma));
            }
        }

        private async System.Threading.Tasks.Task<bool> ApplyGaussianBlurAsync(int kernelSize, double sigma)
        {
            try
            {
                if (_preview == null) return false;
                GaussianBlurParameters parameters = new() { KernelSize = kernelSize, Sigma = sigma };
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GaussianBlur, parameters);
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
            int kernelSize = (int)KernelSizeSlider.Value;
            if (kernelSize % 2 == 0) kernelSize++;
            if (await ApplyGaussianBlurAsync(kernelSize, SigmaSlider.Value) && _preview?.Commit() == true) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _preview?.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            DebounceTimer.Cancel(_debounceKey);
            _preview?.Dispose();
            base.OnClosed(e);
        }
    }
}

