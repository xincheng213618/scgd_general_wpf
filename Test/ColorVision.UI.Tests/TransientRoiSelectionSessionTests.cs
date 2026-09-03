using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class TransientRoiSelectionSessionTests
{
    [Theory]
    [InlineData(SelectShapeType.Rectangle)]
    [InlineData(SelectShapeType.Circle)]
    [InlineData(SelectShapeType.Polygon)]
    [InlineData(SelectShapeType.Quadrilateral)]
    public void WhiteVectorCanvas_AllSelectionShapesCompleteWithoutRequiringPixels(SelectShapeType shape)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            DrawingImage canvas = ImageUtils.CreateSolidColorDrawing(9576, 6388, Colors.White);
            view.SetImageSource(canvas, false, false);
            DrawEditorContext draw = view.EditorContext.DrawEditorContext;
            draw.IsImageEditMode = true;
            draw.Zoombox.Cursor = Cursors.Hand;
            draw.Zoombox.ActivateOn = ModifierKeys.Shift;
            int visualCount = draw.DrawCanvas.Visuals.Count;
            int undoCount = draw.DrawCanvas.UndoStack.Count;
            TransientRoiSelectionSession session = new(draw, shape);
            Task<SelectResult> completion = session.Start();

            Assert.False(completion.IsCompleted);
            Assert.False(draw.IsImageEditMode);
            Assert.Equal(Cursors.Cross, draw.Zoombox.Cursor);
            Assert.Equal(ModifierKeys.Control, draw.Zoombox.ActivateOn);

            DrawingVisual temporary = new();
            draw.DrawCanvas.AddVisual(temporary);
            SetField(session, "_visual", temporary);
            if (shape is SelectShapeType.Rectangle or SelectShapeType.Circle)
                Invoke(session, "TryCompleteDrag", [new Point(10, 20), new Point(70, 90)]);
            else
            {
                SetPoints(session, [new Point(10, 20), new Point(70, 20), new Point(70, 90), new Point(10, 90)]);
                CompleteWithRightClick(session);
            }

            Assert.True(completion.IsCompletedSuccessfully);
            SelectResult result = Assert.IsType<SelectResult>(completion.Result);
            Assert.Equal(shape, result.ShapeType);
            Assert.True(result.Rect.Width > 1 && result.Rect.Height > 1);
            ImageSelectionScope scope = Assert.IsType<ImageSelectionScope>(result.SourceScope);
            Assert.False(scope.HasPixels);
            Assert.Equal((0, 0), (scope.PixelWidth, scope.PixelHeight));
            Assert.Equal((9576d, 6388d), (scope.CanvasWidth, scope.CanvasHeight));
            Assert.True(TransientRoiSelectionSession.IsSourceScopeCurrent(view.EditorContext.ProcessingContext, scope));
            Assert.Same(canvas, view.ViewBitmapSource);
            Assert.Equal(visualCount, draw.DrawCanvas.Visuals.Count);
            Assert.Equal(undoCount, draw.DrawCanvas.UndoStack.Count);
            Assert.True(draw.IsImageEditMode);
            Assert.Equal(Cursors.Hand, draw.Zoombox.Cursor);
            Assert.Equal(ModifierKeys.Shift, draw.Zoombox.ActivateOn);
            Assert.Throws<InvalidOperationException>(() => ImageAlgorithmInputFactory.Acquire(view.EditorContext.ProcessingContext, scope));
        });
    }

    [Theory]
    [InlineData(SelectShapeType.Rectangle, false)]
    [InlineData(SelectShapeType.Rectangle, true)]
    [InlineData(SelectShapeType.Circle, false)]
    [InlineData(SelectShapeType.Circle, true)]
    [InlineData(SelectShapeType.Quadrilateral, false)]
    [InlineData(SelectShapeType.Quadrilateral, true)]
    public void PublicSelectionEntry_CancelsVectorSelectionWhenSourceIsReplaced(SelectShapeType shape, bool replaceWithBitmap)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            view.SetImageSource(ImageUtils.CreateSolidColorDrawing(100, 100, Colors.White), false, false);
            DrawEditorContext draw = view.EditorContext.DrawEditorContext;
            draw.Zoombox.Cursor = Cursors.Hand;
            draw.Zoombox.ActivateOn = ModifierKeys.Shift;
            Task<SelectResult> completion = view.BeginSelectAsync(shape);
            Assert.False(completion.IsCompleted);

            ImageSource replacement = replaceWithBitmap
                ? new WriteableBitmap(100, 100, 96, 96, PixelFormats.Gray8, null)
                : ImageUtils.CreateSolidColorDrawing(100, 100, Colors.White);
            view.SetImageSource(replacement, false, false);

            Assert.True(completion.IsCompletedSuccessfully);
            Assert.Null(completion.Result);
            Assert.False(draw.IsImageEditMode);
            Assert.Equal(Cursors.Hand, draw.Zoombox.Cursor);
            Assert.Equal(ModifierKeys.Shift, draw.Zoombox.ActivateOn);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InvalidInitialSource_DoesNotChangeInteractionState(bool useEmptyDrawing, bool editMode)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            view.ViewBitmapSource = useEmptyDrawing ? new DrawingImage() : null!;
            DrawEditorContext draw = view.EditorContext.DrawEditorContext;
            draw.IsImageEditMode = editMode;
            draw.Zoombox.Cursor = Cursors.Hand;
            draw.Zoombox.ActivateOn = ModifierKeys.Shift;

            Task<SelectResult> completion = view.BeginSelectAsync(SelectShapeType.Rectangle);

            Assert.True(completion.IsCompletedSuccessfully);
            Assert.Null(completion.Result);
            Assert.Equal(editMode, draw.IsImageEditMode);
            Assert.Equal(Cursors.Hand, draw.Zoombox.Cursor);
            Assert.Equal(ModifierKeys.Shift, draw.Zoombox.ActivateOn);
        });
    }

    [Fact]
    public void VectorScope_KeepsFractionalCanvasSizeAndRejectsReuseOnANewBitmap()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            DrawingImage source = new(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 100.5, 50.25))));
            view.SetImageSource(source, false, false);
            ImageProcessingContext context = view.EditorContext.ProcessingContext;
            ImageSelectionScope scope = Assert.IsType<ImageSelectionScope>(TransientRoiSelectionSession.CaptureSourceScope(context));
            Assert.Equal((100.5, 50.25), (scope.CanvasWidth, scope.CanvasHeight));
            Assert.False(scope.HasPixels);

            view.SetImageSource(new WriteableBitmap(100, 50, 96, 96, PixelFormats.Gray8, null), false, false);

            Assert.False(TransientRoiSelectionSession.IsSourceScopeCurrent(context, scope));
            Assert.Throws<InvalidOperationException>(() => ImageAlgorithmInputFactory.Acquire(context, scope));
        });
    }

    [Fact]
    public void BitmapScope_PreservesPixelDimensionsAndDpiSeparatelyFromCanvasSize()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            view.SetImageSource(new WriteableBitmap(120, 80, 192, 192, PixelFormats.Gray8, null), false, false);
            ImageProcessingContext context = view.EditorContext.ProcessingContext;
            ImageSelectionScope scope = Assert.IsType<ImageSelectionScope>(TransientRoiSelectionSession.CaptureSourceScope(context));

            Assert.True(scope.HasPixels);
            Assert.Equal((120, 80, 192d, 192d), (scope.PixelWidth, scope.PixelHeight, scope.DpiX, scope.DpiY));
            Assert.Equal((60d, 40d), (scope.CanvasWidth, scope.CanvasHeight));
            Assert.True(TransientRoiSelectionSession.IsSourceScopeCurrent(context, scope));
            using var pixels = ImageAlgorithmInputFactory.Acquire(context, scope).Image;
            Assert.Equal((120, 80), (pixels.Width, pixels.Height));
        });
    }

    [Fact]
    public void VectorSelection_RightClickCancelRestoresInteractionState()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            view.SetImageSource(ImageUtils.CreateSolidColorDrawing(100, 100, Colors.White), false, false);
            DrawEditorContext draw = view.EditorContext.DrawEditorContext;
            draw.IsImageEditMode = true;
            draw.Zoombox.Cursor = Cursors.Hand;
            draw.Zoombox.ActivateOn = ModifierKeys.Shift;
            TransientRoiSelectionSession session = new(draw, SelectShapeType.Quadrilateral);
            Task<SelectResult> completion = session.Start();
            CompleteWithRightClick(session);

            Assert.True(completion.IsCompletedSuccessfully);
            Assert.Null(completion.Result);
            Assert.True(draw.IsImageEditMode);
            Assert.Equal(Cursors.Hand, draw.Zoombox.Cursor);
            Assert.Equal(ModifierKeys.Shift, draw.Zoombox.ActivateOn);
        });
    }

    [Fact]
    public void VectorSelection_DisposeCancelsAndRemovesTemporaryVisual()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            view.SetImageSource(ImageUtils.CreateSolidColorDrawing(100, 100, Colors.White), false, false);
            DrawEditorContext draw = view.EditorContext.DrawEditorContext;
            TransientRoiSelectionSession session = new(draw, SelectShapeType.Polygon);
            Task<SelectResult> completion = session.Start();
            DrawingVisual temporary = new();
            draw.DrawCanvas.AddVisual(temporary);
            SetField(session, "_visual", temporary);

            view.Dispose();

            Assert.True(completion.IsCompletedSuccessfully);
            Assert.Null(completion.Result);
            Assert.DoesNotContain(temporary, draw.DrawCanvas.Visuals);
        });
    }

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
        => SetField(session, "_polygonPoints", points);

    private static void SetField(TransientRoiSelectionSession session, string field, object value)
        => typeof(TransientRoiSelectionSession).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, value);

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
