using ColorVision.Common.Utilities;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// ThresholdWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ThresholdWindow : System.Windows.Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThresholdWindow));
        private readonly ImageProcessingContext _image;
        private readonly string _debounceKey = $"{nameof(ThresholdWindow)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _preview;

        public ThresholdWindow(ImageProcessingContext image)
        {
            InitializeComponent();
            _image = image;
            _preview = ImageAlgorithmPreviewSession.Start(image);
            
            // 根据图像深度设置最大值
            int maxVal = _image.Config.GetProperties<int>("Max");
            ThresholdSlider.Maximum = maxVal;
            _ = ApplyThresholdAsync();
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, () => _ = ApplyThresholdAsync());
        }

        private async System.Threading.Tasks.Task<bool> ApplyThresholdAsync()
        {
            if (_preview == null)
            {
                return false;
            }

            try
            {
                ThresholdParameters parameters = new() { Threshold = ThresholdSlider.Value };
                AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Threshold, parameters);
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
            if (await ApplyThresholdAsync() && _preview?.Commit() == true) Close();
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

