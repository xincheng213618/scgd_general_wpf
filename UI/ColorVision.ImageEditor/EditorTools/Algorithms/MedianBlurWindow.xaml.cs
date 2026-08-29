using ColorVision.Common.Utilities;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// MedianBlurWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MedianBlurWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MedianBlurWindow));
        private readonly string _debounceKey = $"{nameof(MedianBlurWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;

        public MedianBlurWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            _ = ApplyMedianBlurAsync((int)KernelSizeSlider.Value);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            if (KernelSizeSlider != null)
            {
                int kernelSize = (int)KernelSizeSlider.Value;
                if (kernelSize % 2 == 0)
                {
                    kernelSize += 1;
                    KernelSizeSlider.Value = kernelSize;
                    return;
                }

                DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => _ = ApplyMedianBlurAsync(kernelSize));
            }
        }

        private async System.Threading.Tasks.Task<bool> ApplyMedianBlurAsync(int kernelSize)
        {
            try
            {
                if (_preview == null) return false;
                MedianBlurParameters parameters = new() { KernelSize = kernelSize };
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MedianBlur, parameters);
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
            if (await ApplyMedianBlurAsync(kernelSize) && _preview?.Commit() == true) Close();
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

