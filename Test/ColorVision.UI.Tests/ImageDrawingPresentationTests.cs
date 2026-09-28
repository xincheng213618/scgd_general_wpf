using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ImageDrawingPresentationTests
{
    [Fact]
    public void QueuedFitRunsOnceUsingCurrentLayout()
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new() { Width = 200, Height = 100 };
            Zoombox zoom = new() { Child = canvas };
            DrawEditorContext context = new(canvas, zoom);
            using ImageDrawingPresentation presentation = new(context, new ImageViewConfig());
            presentation.Attach();
            zoom.Measure(new Size(400, 300));
            zoom.Arrange(new Rect(0, 0, 400, 300));
            int changes = 0;
            zoom.ContentMatrixChanged += (_, _) => changes++;
            StaTest.Run(() => { for (int i = 0; i < 100; i++) presentation.ZoomToFit(); });
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            Assert.Equal(new Matrix(2, 0, 0, 2, 0, 50), zoom.ContentMatrix);
            Assert.Equal(0.5, canvas.Scale);
            Assert.Equal(1, changes);
            context.MouseInfoProvider.Dispose();
        });
    }

    [Fact]
    public void UnloadAbortsQueuedFitAndReloadAllowsNewFit()
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new() { Width = 200, Height = 100 };
            Zoombox zoom = new() { Child = canvas };
            DrawEditorContext context = new(canvas, zoom);
            using ImageDrawingPresentation presentation = new(context, new ImageViewConfig());
            presentation.Attach();
            Window window = new() { Content = zoom, Width = 400, Height = 300,
                Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                DispatcherOperation? pendingLayout = null;
                DispatcherHookEventHandler onPosted = (_, e) =>
                {
                    if (e.Operation.Priority == DispatcherPriority.ContextIdle) pendingLayout = e.Operation;
                };
                Dispatcher.CurrentDispatcher.Hooks.OperationPosted += onPosted;
                try { presentation.ZoomToFit(); }
                finally { Dispatcher.CurrentDispatcher.Hooks.OperationPosted -= onPosted; }
                Assert.NotNull(pendingLayout);
                window.Content = null;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.False(zoom.IsLoaded);
                Assert.Equal(DispatcherOperationStatus.Aborted, pendingLayout.Status);
                zoom.RestoreView(Matrix.Identity);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.Equal(Matrix.Identity, zoom.ContentMatrix);

                window.Content = zoom;
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                presentation.ZoomToFit();
                Assert.Equal(Math.Min(zoom.ActualWidth / 200, zoom.ActualHeight / 100), zoom.ContentMatrix.M11, 10);
            }
            finally { window.Close(); context.MouseInfoProvider.Dispose(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedFitRequestsKeepOneQueuedOperationAndDisposeAbortsIt(bool fromWorker)
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new() { Width = 200, Height = 120 };
            Zoombox zoom = new() { Child = canvas };
            DrawEditorContext context = new(canvas, zoom);
            using ImageDrawingPresentation presentation = new(context, new ImageViewConfig());
            presentation.Attach();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            List<DispatcherOperation> posted = [];
            DispatcherPriority priority = fromWorker ? DispatcherPriority.Normal : DispatcherPriority.ContextIdle;
            DispatcherHookEventHandler onPosted = (_, e) => { if (e.Operation.Priority == priority) posted.Add(e.Operation); };
            dispatcher.Hooks.OperationPosted += onPosted;
            try
            {
                void RequestFits() { for (int i = 0; i < 100; i++) presentation.ZoomToFit(); }
                if (fromWorker) StaTest.Run(RequestFits);
                else RequestFits();

                DispatcherOperation pending = Assert.Single(posted, operation => operation.Status == DispatcherOperationStatus.Pending);
                presentation.Dispose();
                Assert.Equal(DispatcherOperationStatus.Aborted, pending.Status);
            }
            finally
            {
                dispatcher.Hooks.OperationPosted -= onPosted;
                foreach (DispatcherOperation operation in posted) operation.Abort();
                context.MouseInfoProvider.Dispose();
                zoom.Child = null;
            }
        });
    }
}
