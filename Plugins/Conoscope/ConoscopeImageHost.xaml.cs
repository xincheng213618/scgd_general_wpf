using ColorVision.ImageEditor;
using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Conoscope
{
    public partial class ConoscopeImageHost : UserControl, IDisposable
    {
        public ConoscopeImageHost()
        {
            InitializeComponent();
            ZoomBox.ContentMatrixChanged += (_, _) => UpdateDrawingVisualScale();
        }

        public DrawCanvas ImageShow => ImageCanvas;

        public Zoombox Zoombox1 => ZoomBox;

        public void Clear()
        {
            ImageCanvas.Clear();
            ImageCanvas.Source = null;
            ImageCanvas.UpdateLayout();
        }

        public void SetImageSource(ImageSource imageSource)
        {
            ImageCanvas.Source = imageSource;
            ImageCanvas.RaiseImageInitialized();
        }

        public void UpdateZoomAndScale()
        {
            if (CheckAccess())
            {
                UpdateZoomAndScaleCore();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(UpdateZoomAndScaleCore));
            }
        }

        private void UpdateZoomAndScaleCore()
        {
            ZoomBox.ZoomUniform();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                UpdateDrawingVisualScale();
                ImageCanvas.ApplyLayoutScaleToVisuals();
            }));
        }

        private void UpdateDrawingVisualScale()
        {
            double zoomRatio = ZoomBox.ContentMatrix.M11;
            ImageCanvas.Sacle = double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0 ? 1 : 1 / zoomRatio;
        }

        public void Dispose()
        {
            Clear();
            ZoomBox.Child = null;
        }
    }
}