using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using OpenCvSharp;
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
            ApplyThreshold();
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized || _preview == null)
            {
                return;
            }

            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, ApplyThreshold);
        }

        private void ApplyThreshold()
        {
            if (_preview == null)
            {
                return;
            }

            try
            {
                double thresh = ThresholdSlider.Value;
                double maxval = _image.Config.GetProperties<int>("Max");
                _preview.Apply(mat => OpenCvImageAlgorithms.Threshold(mat, thresh, maxval, ThresholdTypes.Binary));
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

