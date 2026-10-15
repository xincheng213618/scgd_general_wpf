using ColorVision.ImageEditor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class ZoomboxLifecycleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void UnarrangedDetachedContentDoesNotQueueDispatcherRetries(int mode)
    {
        StaTest.Run(() =>
        {
            Zoombox zoom = new() { Child = new Border { Width = 200, Height = 120 } };
            zoom.Measure(new Size(400, 300));
            zoom.Arrange(new Rect(0, 0, 400, 300));
            zoom.Child.InvalidateArrange();
            List<DispatcherOperation> posted = [];
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherHookEventHandler onPosted = (_, e) => posted.Add(e.Operation);
            dispatcher.Hooks.OperationPosted += onPosted;
            try
            {
                RequestZoom(zoom, mode);
                Assert.Empty(posted);
            }
            finally
            {
                dispatcher.Hooks.OperationPosted -= onPosted;
                foreach (DispatcherOperation operation in posted) operation.Abort();
                zoom.Child = null;
            }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ZoomRequestedBeforeLoadRunsWhenLayoutBecomesReady(int mode)
    {
        WpfTestHost.Invoke(() =>
        {
            Border child = new() { Width = 200, Height = 120 };
            Zoombox zoom = new() { Child = child, ContentMatrix = Matrix.Identity };
            RequestZoom(zoom, mode);
            Window window = CreateWindow(zoom);
            try
            {
                window.Show();
                window.UpdateLayout();
                DrainBackground();
                AssertExpectedZoom(zoom, child, mode);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void DeferredZoomCoalescesToLatestRequestAndRunsOnlyOnce()
    {
        WpfTestHost.Invoke(() =>
        {
            Border child = new() { Width = 200, Height = 120 };
            Zoombox zoom = new() { Child = child, ContentMatrix = Matrix.Identity };
            Window window = CreateWindow(zoom);
            try
            {
                window.Show();
                window.UpdateLayout();
                DrainBackground();
                int changes = 0;
                zoom.ContentMatrixChanged += (_, _) => changes++;
                child.Width = 240;
                child.InvalidateArrange();
                zoom.ZoomUniform();
                zoom.ZoomUniformToFill();
                RequestZoom(zoom, 2);
                Assert.Equal(0, changes);

                window.UpdateLayout();
                DrainBackground();
                AssertExpectedZoom(zoom, child, 2);
                Assert.Equal(1, changes);

                child.Width = 260;
                window.UpdateLayout();
                DrainBackground();
                Assert.Equal(1, changes);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void UnloadCancelsPendingZoomAndReloadAcceptsNewRequests()
    {
        WpfTestHost.Invoke(() =>
        {
            Border child = new() { Width = 200, Height = 120 };
            Zoombox zoom = new() { Child = child, ContentMatrix = Matrix.Identity };
            Window window = CreateWindow(zoom);
            try
            {
                window.Show();
                window.UpdateLayout();
                DrainBackground();
                child.InvalidateArrange();
                zoom.ZoomUniform();
                window.Content = null;
                DrainBackground();
                Assert.False(zoom.IsLoaded);

                window.Content = zoom;
                window.UpdateLayout();
                DrainBackground();
                Assert.Equal(Matrix.Identity, zoom.ContentMatrix);

                zoom.ZoomUniformToFill();
                AssertExpectedZoom(zoom, child, 1);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ReplacingChildDiscardsZoomRequestedForPreviousContent()
    {
        WpfTestHost.Invoke(() =>
        {
            Zoombox zoom = new() { Child = new Border { Width = 200, Height = 120 }, ContentMatrix = Matrix.Identity };
            zoom.ZoomUniform();
            zoom.Child = new Border { Width = 40, Height = 30 };
            Window window = CreateWindow(zoom);
            try
            {
                window.Show();
                window.UpdateLayout();
                DrainBackground();
                Assert.Equal(Matrix.Identity, zoom.ContentMatrix);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ExplicitNavigationSupersedesDeferredFit(int operation)
    {
        WpfTestHost.Invoke(() =>
        {
            Zoombox zoom = new() { Child = new Border { Width = 200, Height = 120 } };
            zoom.ZoomUniform();
            switch (operation)
            {
                case 0: zoom.ZoomNone(); break;
                case 1: zoom.RestoreView(new Matrix(3, 0, 0, 3, 17, 19)); break;
                case 2: zoom.Zoom(new Point(20, 30), new Vector(2, 2)); break;
                case 3: zoom.Pan(new Vector(17, 19)); break;
                case 4: zoom.ContentMatrix = Matrix.Identity; break;
                case 5: zoom.SetCurrentValue(Zoombox.ContentMatrixProperty, new Matrix(2, 0, 0, 2, 5, 7)); break;
            }
            Matrix requested = zoom.ContentMatrix;
            Window window = CreateWindow(zoom);
            try
            {
                window.Show();
                window.UpdateLayout();
                DrainBackground();
                Assert.Equal(requested, zoom.ContentMatrix);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void NonuniformZoomUsesEachAxesOwnLimit()
    {
        StaTest.Run(() =>
        {
            Zoombox zoom = new() { MinZoom = 1, MaxZoom = 5, ContentMatrix = new Matrix(2, 0, 0, 4, 0, 0) };
            zoom.Zoom(new Point(), new Vector(2, 2));
            Assert.Equal(new Matrix(4, 0, 0, 5, 0, 0), zoom.ContentMatrix);
            zoom.Zoom(new Point(), new Vector(0.01, 0.01));
            Assert.Equal(Matrix.Identity, zoom.ContentMatrix);
        });
    }

    [Fact]
    public void ZoomKeepsAnchorFixedAndPanUsesViewportCoordinates()
    {
        StaTest.Run(() =>
        {
            Zoombox zoom = new() { ContentMatrix = new Matrix(2, 0.3, 0.4, 3, 17, 19) };
            Point anchor = new(80, 50);
            Matrix inverse = zoom.ContentMatrix;
            inverse.Invert();
            Point contentPoint = inverse.Transform(anchor);
            zoom.Zoom(anchor, new Vector(1.5, 2));
            Point afterZoom = zoom.ContentMatrix.Transform(contentPoint);
            Assert.Equal(anchor.X, afterZoom.X, 10);
            Assert.Equal(anchor.Y, afterZoom.Y, 10);

            zoom.Pan(new Vector(7, -11));
            Point afterPan = zoom.ContentMatrix.Transform(contentPoint);
            Assert.Equal(anchor.X + 7, afterPan.X, 10);
            Assert.Equal(anchor.Y - 11, afterPan.Y, 10);
        });
    }

    [Fact]
    public void UnchangedViewDoesNotNotifySubscribers()
    {
        StaTest.Run(() =>
        {
            Zoombox zoom = new() { MaxZoom = 1 };
            int changes = 0;
            zoom.ContentMatrixChanged += (_, _) => changes++;
            for (int i = 0; i < 100; i++) zoom.Zoom(new Point(20, 30), new Vector(2, 2));
            zoom.Pan(default);
            zoom.ZoomNone();
            zoom.RestoreView(Matrix.Identity);
            Assert.Equal(Matrix.Identity, zoom.ContentMatrix);
            Assert.Equal(0, changes);
        });
    }

    [Fact]
    public void BoundMatrixAndNavigationNotifyOnceAndKeepBinding()
    {
        StaTest.Run(() =>
        {
            MatrixTransform source = new();
            Zoombox zoom = new();
            BindingOperations.SetBinding(zoom, Zoombox.ContentMatrixProperty, new Binding(nameof(MatrixTransform.Matrix)) { Source = source });
            int changes = 0;
            zoom.ContentMatrixChanged += (_, _) => changes++;

            source.Matrix = new Matrix(2, 0, 0, 3, 4, 5);
            Assert.Equal(source.Matrix, zoom.ContentMatrix);
            Assert.Equal(1, changes);
            zoom.Pan(new Vector(3, 7));
            Assert.Equal(2, changes);
            Assert.True(BindingOperations.IsDataBound(zoom, Zoombox.ContentMatrixProperty));
            source.Matrix = new Matrix(4, 0, 0, 5, 6, 7);
            Assert.Equal(source.Matrix, zoom.ContentMatrix);
            Assert.Equal(3, changes);

            zoom.ContentMatrix = Matrix.Identity;
            Assert.Equal(4, changes);
        });
    }

    [Fact]
    public void SeparateUiThreadsCanZoomIndependently()
    {
        for (int i = 0; i < 2; i++)
        {
            StaTest.Run(() =>
            {
                Zoombox zoom = new();
                zoom.Zoom(new Point(10, 20), new Vector(2, 3));
                zoom.Pan(new Vector(4, 5));
                Assert.Equal(new Matrix(2, 0, 0, 3, -6, -35), zoom.ContentMatrix);
            });
        }
    }

    private static Window CreateWindow(UIElement content) => new()
    {
        Content = content, Width = 400, Height = 300,
        Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false
    };

    private static void RequestZoom(Zoombox zoom, int mode)
    {
        if (mode == 0) zoom.ZoomUniform();
        else if (mode == 1) zoom.ZoomUniformToFill();
        else zoom.ZoomToContentRect(new Rect(20, 10, 80, 40));
    }

    private static void AssertExpectedZoom(Zoombox zoom, Border child, int mode)
    {
        double width = mode == 2 ? 80 : child.Width;
        double height = mode == 2 ? 40 : child.Height;
        double scale = mode == 1
            ? Math.Max(zoom.ActualWidth / width, zoom.ActualHeight / height)
            : Math.Min(zoom.ActualWidth / width, zoom.ActualHeight / height);
        Assert.Equal(scale, zoom.ContentMatrix.M11, 10);
        Assert.Equal(scale, zoom.ContentMatrix.M22, 10);
        Assert.Equal((zoom.ActualWidth - scale * width) / 2 - (mode == 2 ? 20 * scale : 0), zoom.ContentMatrix.OffsetX, 10);
        Assert.Equal((zoom.ActualHeight - scale * height) / 2 - (mode == 2 ? 10 * scale : 0), zoom.ContentMatrix.OffsetY, 10);
    }

    private static void DrainBackground() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
}
