using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class TransientRoiSelectionSessionTests
{
    [Fact]
    public void ExistingDrawingScopeRejectsARevisionChangedDuringParameterEditing()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            try
            {
                view.SetImageSource(new WriteableBitmap(100, 100, 96, 96, System.Windows.Media.PixelFormats.Gray8, null), false, false);
                ImageProcessingContext context = view.EditorContext.ProcessingContext;
                ImageSelectionScope scope = Assert.IsType<ImageSelectionScope>(TransientRoiSelectionSession.CaptureSourceScope(context));

                view.SetImageSource(new WriteableBitmap(100, 100, 96, 96, System.Windows.Media.PixelFormats.Gray8, null), false, false);

                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ImageAlgorithmInputFactory.Acquire(context, scope));
                Assert.Contains("changed", error.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                view.Dispose();
            }
        });
    }

    [Fact]
    public void CompletedSelectionCarriesImmutableScopeAndSnapshotAcquireRejectsLaterRevision()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            try
            {
                view.SetImageSource(new WriteableBitmap(100, 100, 96, 96, System.Windows.Media.PixelFormats.Gray8, null), false, false);
                TransientRoiSelectionSession session = new(view.EditorContext.DrawEditorContext, SelectShapeType.Polygon);
                Task<SelectResult> completion = session.Start();
                SetPoints(session, [new Point(0, 0), new Point(10, 0), new Point(0, 10)]);
                CompleteWithRightClick(session);

                ImageSelectionScope scope = Assert.IsType<ImageSelectionScope>(completion.Result.SourceScope);
                Assert.Equal(view.EditorContext.ProcessingContext.DocumentInstanceId, scope.DocumentInstanceId);
                Assert.Equal(view.ImageRevision, scope.SourceRevision);
                Assert.Equal((100, 100, 96d, 96d), (scope.PixelWidth, scope.PixelHeight, scope.DpiX, scope.DpiY));

                view.SetImageSource(new WriteableBitmap(100, 100, 96, 96, System.Windows.Media.PixelFormats.Gray8, null), false, false);
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                    () => ImageAlgorithmInputFactory.Acquire(view.EditorContext.ProcessingContext, scope));
                Assert.Contains("changed", error.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                view.Dispose();
            }
        });
    }

    [Fact]
    public void DisposeCancelsBoundSelectionAndRemovesTransientVisual()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            view.SetImageSource(new WriteableBitmap(20, 20, 96, 96, System.Windows.Media.PixelFormats.Gray8, null), false, false);
            TransientRoiSelectionSession session = new(view.EditorContext.DrawEditorContext, SelectShapeType.Polygon);
            Task<SelectResult> completion = session.Start();
            SetPoints(session, [new Point(0, 0), new Point(10, 0), new Point(0, 10)]);

            view.Dispose();

            Assert.True(completion.IsCompletedSuccessfully);
            Assert.Null(completion.Result);
        });
    }

    [Fact]
    public void ImageRevisionChangeCancelsBoundSelectionBeforeCoordinatesCanBeReused()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            try
            {
                view.SetImageSource(new WriteableBitmap(100, 100, 96, 96, System.Windows.Media.PixelFormats.Gray8, null), false, false);
                TransientRoiSelectionSession session = new(view.EditorContext.DrawEditorContext, SelectShapeType.Polygon);
                Task<SelectResult> completion = session.Start();

                view.SetImageSource(new WriteableBitmap(200, 200, 192, 192, System.Windows.Media.PixelFormats.Gray8, null), false, false);
                if (!completion.IsCompleted)
                {
                    SetPoints(session, [new Point(0, 0), new Point(99, 0), new Point(0, 99)]);
                    CompleteWithRightClick(session);
                }

                Assert.True(completion.IsCompletedSuccessfully);
                Assert.Null(completion.Result);
            }
            finally
            {
                view.Dispose();
            }
        });
    }

    [Fact]
    public void SelectionGeometryRejectsDegenerateAndSelfIntersectingShapes()
    {
        Assert.False(TransientRoiSelectionSession.IsValidPolygon([new Point(0, 0), new Point(10, 0)]));
        Assert.False(TransientRoiSelectionSession.IsValidPolygon([new Point(0, 0), new Point(10, 0), new Point(20, 0)]));
        Assert.False(TransientRoiSelectionSession.IsValidPolygon(
            [new Point(0, 0), new Point(10, 10), new Point(0, 10), new Point(10, 0)]));
        Assert.False(TransientRoiSelectionSession.IsValidPolygon(
            [new Point(0, 0), new Point(double.NaN, 10), new Point(10, 0)]));
        Assert.True(TransientRoiSelectionSession.IsValidPolygon(
            [new Point(0, 0), new Point(10, 0), new Point(0, 10)]));

        Assert.False(TransientRoiSelectionSession.IsValidDragResult(new SelectResult
        {
            ShapeType = SelectShapeType.Rectangle,
            Rect = new Rect(0, 0, 0, 10),
        }));
        Assert.False(TransientRoiSelectionSession.IsValidDragResult(new SelectResult
        {
            ShapeType = SelectShapeType.Circle,
            Rect = Rect.Empty,
        }));
    }

    [Fact]
    public void PolygonCompletionRejectsInvalidGeometryAndKeepsSessionActive()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new();
            DrawEditorContext context = new(canvas, zoombox);
            TransientRoiSelectionSession session = new(context, SelectShapeType.Polygon);
            Task<SelectResult> completion = session.Start();

            try
            {
                SetPoints(session, [new Point(0, 0), new Point(10, 0)]);
                CompleteWithRightClick(session);
                Assert.False(completion.IsCompleted);
                Assert.Equal(Cursors.Cross, zoombox.Cursor);

                SetPoints(session, [new Point(0, 0), new Point(10, 0), new Point(20, 0)]);
                CompleteWithRightClick(session);
                Assert.False(completion.IsCompleted);

                SetPoints(session, [new Point(0, 0), new Point(10, 0), new Point(0, 10)]);
                CompleteWithRightClick(session);
                Assert.True(completion.IsCompletedSuccessfully);
                Assert.Equal(3, completion.Result.Points.Count);
            }
            finally
            {
                if (!completion.IsCompleted)
                {
                    Invoke(session, "Cleanup");
                }
            }
        });
    }

    private static void SetPoints(TransientRoiSelectionSession session, List<Point> points)
        => typeof(TransientRoiSelectionSession)
            .GetField("_polygonPoints", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, points);

    private static void CompleteWithRightClick(TransientRoiSelectionSession session)
    {
        MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
        {
            RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent,
        };
        Invoke(session, "OnMouseRightDown", new object[] { session, args });
        Assert.True(args.Handled);
    }

    private static void Invoke(TransientRoiSelectionSession session, string method, object[]? parameters = null)
        => typeof(TransientRoiSelectionSession)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(session, parameters ?? Array.Empty<object>());

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
