using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    internal sealed class CameraAxesUpdateCoordinator : IDisposable
    {
        private readonly HelixViewport3D viewport;
        private readonly IReadOnlyList<Visual3D> axes;
        private DispatcherOperation? pendingUpdate;
        private bool isDisposed;

        public CameraAxesUpdateCoordinator(HelixViewport3D viewport, IReadOnlyList<Visual3D> axes)
        {
            ArgumentNullException.ThrowIfNull(viewport);
            ArgumentNullException.ThrowIfNull(axes);

            this.viewport = viewport;
            this.axes = axes;

            viewport.CameraChanged += Viewport_CameraChanged;
            RequestUpdate();
        }

        private void Viewport_CameraChanged(object sender, RoutedEventArgs e)
        {
            RequestUpdate();
        }

        public void RequestUpdate()
        {
            if (isDisposed ||
                viewport.Dispatcher.HasShutdownStarted ||
                viewport.Dispatcher.HasShutdownFinished ||
                pendingUpdate?.Status == DispatcherOperationStatus.Pending)
            {
                return;
            }

            pendingUpdate = viewport.Dispatcher.InvokeAsync(() =>
            {
                pendingUpdate = null;
                if (!isDisposed && viewport.Camera is ProjectionCamera camera)
                {
                    Viewport3DHelper.UpdateFixedCornerAxes(axes, camera);
                }
            }, DispatcherPriority.Render);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            viewport.CameraChanged -= Viewport_CameraChanged;

            DispatcherOperation? operation = pendingUpdate;
            pendingUpdate = null;
            if (operation?.Status == DispatcherOperationStatus.Pending)
            {
                operation.Abort();
            }
        }
    }
}
