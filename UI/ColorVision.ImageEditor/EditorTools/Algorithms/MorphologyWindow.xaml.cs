using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using log4net;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public partial class MorphologyWindow : System.Windows.Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MorphologyWindow));
        private readonly string _debounceKey = $"{nameof(MorphologyWindow)}_{Guid.NewGuid():N}";
        private readonly ImageAlgorithmPreviewSession _preview;

        public MorphologyWindow(ImageProcessingContext image, int defaultOperation = 0)
        {
            InitializeComponent();
            _preview = ImageAlgorithmPreviewSession.Start(image);
            OperationCombo.SelectedIndex = defaultOperation;
            ApplyPreview();
        }

        private void Param_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
            {
                return;
            }

            DebounceTimer.AddOrResetTimerDispatcher(_debounceKey, 50, ApplyPreview);
        }

        private void ApplyPreview()
        {
            try
            {
                int kernelSize = (int)KernelSlider.Value;
                int iterations = Math.Max(1, (int)IterSlider.Value);
                MorphologyOperation operation = (MorphologyOperation)Math.Max(0, OperationCombo.SelectedIndex);

                _preview.Apply(mat => OpenCvImageAlgorithms.Morphology(mat, operation, kernelSize, iterations));
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
