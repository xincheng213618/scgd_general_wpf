using ColorVision.Common.Utilities;
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
            ApplyMedianBlur((int)KernelSizeSlider.Value);
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

                DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => ApplyMedianBlur(kernelSize));
            }
        }

        private void ApplyMedianBlur(int kernelSize)
        {
            try
            {
                _preview?.Apply(mat => OpenCvImageAlgorithms.MedianBlur(mat, kernelSize));
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

