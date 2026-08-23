using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Annotations;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class DrawMarkerToolTests
{
    [Fact]
    public void DeactivatingDragToolReleasesMouseCapture()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            CountingDragTool manager = new(context);
            Window window = new()
            {
                Content = zoombox,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 400,
                Height = 300,
                Left = -10000,
                Top = -10000,
            };

            try
            {
                window.Show();
                manager.IsChecked = true;
                BeginDragGesture(manager, drawCanvas);
                Assert.True(drawCanvas.IsMouseCaptured);

                manager.IsChecked = false;

                Assert.False(drawCanvas.IsMouseCaptured);
                Assert.Null(context.DrawEditorManager.Current);
            }
            finally
            {
                window.Close();
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void DisposingInactiveDragToolDoesNotReleaseAnotherToolsMouseCapture()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            CountingDragTool manager = new(context);
            Window window = new()
            {
                Content = zoombox,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 400,
                Height = 300,
                Left = -10000,
                Top = -10000,
            };

            try
            {
                window.Show();
                drawCanvas.CaptureMouse();
                Assert.True(drawCanvas.IsMouseCaptured);

                manager.Dispose();

                Assert.True(drawCanvas.IsMouseCaptured);
            }
            finally
            {
                drawCanvas.ReleaseMouseCapture();
                window.Close();
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void DragToolIgnoresMouseUpWhenNoGestureWasStarted()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            CountingDragTool manager = new(context);

            try
            {
                manager.IsChecked = true;
                MouseButtonEventArgs mouseUp = CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent);
                MethodInfo handler = typeof(DragDrawingToolBase).GetMethod("HandlePreviewMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;

                handler.Invoke(manager, new object[] { drawCanvas, mouseUp });

                Assert.Equal(0, manager.EndDrawCount);
                Assert.False(mouseUp.Handled);
            }
            finally
            {
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void IdleDrawingToolsDoNotConsumeSharedCanvasMouseMove()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            CountingDragTool dragTool = new(context);
            PolygonManager multiPointTool = new(context);
            EraseManager eraseTool = new(context);

            try
            {
                MouseEventArgs dragMove = CreateMouseMoveArgs();
                MouseEventArgs multiPointMove = CreateMouseMoveArgs();
                MouseEventArgs eraseMove = CreateMouseMoveArgs();

                typeof(DragDrawingToolBase).GetMethod("HandleMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(dragTool, new object[] { drawCanvas, dragMove });
                typeof(MultiPointDrawingToolBase<DVPolygon>).GetMethod("HandleMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(multiPointTool, new object[] { drawCanvas, multiPointMove });
                typeof(EraseManager).GetMethod("MouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(eraseTool, new object[] { drawCanvas, eraseMove });

                Assert.False(dragMove.Handled);
                Assert.False(multiPointMove.Handled);
                Assert.False(eraseMove.Handled);
            }
            finally
            {
                dragTool.Dispose();
                multiPointTool.Dispose();
                eraseTool.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void DragToolDoesNotEndALeftGestureWhenTheRightButtonIsReleased()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            CountingDragTool manager = new(context);

            try
            {
                manager.IsChecked = true;
                BeginDragGesture(manager, drawCanvas);
                MethodInfo handler = typeof(DragDrawingToolBase).GetMethod("HandlePreviewMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
                MouseButtonEventArgs rightMouseUp = CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent, MouseButton.Right);

                handler.Invoke(manager, new object[] { drawCanvas, rightMouseUp });

                Assert.Equal(0, manager.EndDrawCount);
                Assert.False(rightMouseUp.Handled);

                handler.Invoke(manager, new object[] { drawCanvas, CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent) });
                Assert.Equal(1, manager.EndDrawCount);
            }
            finally
            {
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultiPointCreationRemovedByVisualsAddLeavesNoGhostOrHistory(bool useRemovalCommand)
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            PolygonManager manager = new(context);
            drawCanvas.VisualsAdd += RemoveNewPolygon;

            try
            {
                manager.IsChecked = true;
                BeginPolygonGesture(manager, drawCanvas);

                Assert.Empty(drawCanvas.Visuals.OfType<DVPolygon>());
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);
                Assert.Null(GetActivePolygon(manager));
            }
            finally
            {
                drawCanvas.VisualsAdd -= RemoveNewPolygon;
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }

            void RemoveNewPolygon(object? sender, VisualChangedEventArgs e)
            {
                if (e.Visual is DVPolygon polygon)
                {
                    if (useRemovalCommand)
                        drawCanvas.RemoveVisualCommand(polygon);
                    else
                        drawCanvas.RemoveVisual(polygon);
                }
            }
        });
    }

    [Fact]
    public void MultiPointCreationInterruptedByToolSwitchLeavesNoGhostOrHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            PolygonManager manager = new(context);
            drawCanvas.VisualsAdd += SwitchTool;

            try
            {
                manager.IsChecked = true;
                BeginPolygonGesture(manager, drawCanvas);

                Assert.False(manager.IsChecked);
                Assert.Null(context.DrawEditorManager.Current);
                Assert.Empty(drawCanvas.Visuals.OfType<DVPolygon>());
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);
                Assert.Null(GetActivePolygon(manager));
            }
            finally
            {
                drawCanvas.VisualsAdd -= SwitchTool;
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }

            void SwitchTool(object? sender, VisualChangedEventArgs e)
            {
                if (e.Visual is DVPolygon)
                    manager.IsChecked = false;
            }
        });
    }

    [Fact]
    public void MultiPointToolDoesNotConsumeRightMouseUpDuringALeftClickGesture()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            PolygonManager manager = new(context);

            try
            {
                manager.IsChecked = true;
                BeginPolygonGesture(manager, drawCanvas);
                DVPolygon activePolygon = Assert.IsType<DVPolygon>(GetActivePolygon(manager));
                MethodInfo mouseUpHandler = typeof(MultiPointDrawingToolBase<DVPolygon>).GetMethod("HandlePreviewMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
                MouseButtonEventArgs rightMouseUp = CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent, MouseButton.Right);

                mouseUpHandler.Invoke(manager, new object[] { drawCanvas, rightMouseUp });

                Assert.False(rightMouseUp.Handled);
                Assert.Same(activePolygon, GetActivePolygon(manager));

                MouseButtonEventArgs leftMouseUp = CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent);
                mouseUpHandler.Invoke(manager, new object[] { drawCanvas, leftMouseUp });
                Assert.True(leftMouseUp.Handled);
            }
            finally
            {
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void BrushStrokeUsesOneStreamGeometryForManyPoints()
    {
        WpfTestHost.Invoke(() =>
        {
            BrushStrokeProperties properties = new()
            {
                Pen = new Pen(Brushes.OrangeRed, 4),
            };
            for (int index = 0; index < 1_000; index++)
            {
                properties.Points.Add(new Point(index * 0.25, 40 + Math.Sin(index * 0.05) * 12));
            }

            DVBrushStroke stroke = new(properties);
            stroke.Render();

            DrawingGroup drawing = Assert.IsType<DrawingGroup>(stroke.Drawing);
            GeometryDrawing geometryDrawing = Assert.IsType<GeometryDrawing>(Assert.Single(drawing.Children));
            Assert.IsType<StreamGeometry>(geometryDrawing.Geometry);
        });
    }

    [Fact]
    public void BrushConfigurationSerializationPreservesEffectiveOpacity()
    {
        WpfTestHost.Invoke(() =>
        {
            BrushManagerConfig source = new()
            {
                StrokeBrush = new SolidColorBrush(Colors.Red) { Opacity = 0.35 },
            };
            BrushManagerConfig restored = new()
            {
                SerializedStrokeBrush = source.SerializedStrokeBrush,
            };

            SolidColorBrush restoredBrush = Assert.IsType<SolidColorBrush>(restored.StrokeBrush);
            Assert.Equal((byte)89, restoredBrush.Color.A);
            Assert.Equal(Colors.Red.R, restoredBrush.Color.R);
            Assert.Equal(Colors.Red.G, restoredBrush.Color.G);
            Assert.Equal(Colors.Red.B, restoredBrush.Color.B);
            Assert.Equal(1, restoredBrush.Opacity);
        });
    }

    [Fact]
    public void BrushStrokeDoesNotMutateOrRejectAFrozenPen()
    {
        WpfTestHost.Invoke(() =>
        {
            Pen frozenPen = new(Brushes.Gold, 3);
            frozenPen.Freeze();
            BrushStrokeProperties properties = new()
            {
                Pen = frozenPen,
                Points = new List<Point> { new(5, 5), new(30, 25), new(60, 10) },
            };

            DVBrushStroke stroke = new(properties);
            stroke.Render();

            Assert.Same(frozenPen, properties.Pen);
            Assert.Equal(PenLineCap.Flat, frozenPen.StartLineCap);
            Assert.Equal(PenLineCap.Flat, frozenPen.EndLineCap);
        });
    }

    [Fact]
    public void BrushLayoutScaleRendersOnceAfterPreparingAFrozenPen()
    {
        WpfTestHost.Invoke(() =>
        {
            Pen frozenPen = new(Brushes.Gold, 3);
            frozenPen.Freeze();
            BrushStrokeProperties properties = new()
            {
                Pen = frozenPen,
                ScreenThickness = 4,
                Points = new List<Point> { new(5, 5), new(30, 25), new(60, 10) },
            };
            CountingBrushStroke stroke = new(properties);

            stroke.ApplyLayoutScale(new DrawingVisualScaleContext(true, 2, 0));

            Assert.Equal(1, stroke.RenderCount);
            Assert.NotSame(frozenPen, properties.Pen);
            Assert.Equal(8, properties.Pen.Thickness);
            Assert.Equal(3, frozenPen.Thickness);
        });
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void BrushManagerSamplesAtConfiguredScreenSpacing(double zoomRatio)
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = new Matrix(zoomRatio, 0, 0, zoomRatio, 0, 0) };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);
            manager.Config.SampleSpacing = 4;
            double documentSpacing = manager.Config.SampleSpacing / zoomRatio;

            try
            {
                InvokeBrushMethod(manager, "OnBeginDraw", new Point(10, 20), CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent));
                DVBrushStroke stroke = GetCurrentStroke(manager);
                MouseEventArgs moveArgs = CreateMouseMoveArgs();
                InvokeBrushMethod(manager, "OnUpdateDraw", new Point(10 + documentSpacing - 0.01, 20), moveArgs);
                Assert.False(GetPrivateField<bool>(manager, "_previewRenderPending"));
                InvokeBrushMethod(manager, "OnUpdateDraw", new Point(10 + documentSpacing, 20), moveArgs);
                Assert.True(GetPrivateField<bool>(manager, "_previewRenderPending"));
                InvokeBrushMethod(manager, "OnEndDraw", new Point(10 + documentSpacing, 20), CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent));

                Assert.Equal(new[] { new Point(10, 20), new Point(10 + documentSpacing, 20) }, stroke.Points);
                Assert.Single(drawCanvas.UndoStack);
            }
            finally
            {
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void BrushManagerCoalescesPreviewRenderingToOneCompositionFrame()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);
            int addCount = 0;
            int removeCount = 0;
            drawCanvas.VisualsAdd += (_, e) => addCount += e.Visuals.Count;
            drawCanvas.VisualsRemove += (_, e) => removeCount += e.Visuals.Count;

            try
            {
                InvokeBrushMethod(manager, "OnBeginDraw", new Point(10, 30), CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent));
                DVBrushStroke stroke = GetCurrentStroke(manager);
                Rect initialDrawingBounds = Assert.IsAssignableFrom<Drawing>(stroke.Drawing).Bounds;
                Assert.Equal(0, addCount);
                Assert.Equal(0, removeCount);
                Assert.Empty(drawCanvas.UndoStack);
                MouseEventArgs moveArgs = CreateMouseMoveArgs();
                for (int index = 1; index <= 80; index++)
                {
                    InvokeBrushMethod(manager, "OnUpdateDraw", new Point(10 + index * 3, 30 + index % 5), moveArgs);
                }

                Assert.True(GetPrivateField<bool>(manager, "_previewRenderPending"));
                Assert.Equal(initialDrawingBounds, Assert.IsAssignableFrom<Drawing>(stroke.Drawing).Bounds);
                Assert.True(stroke.Points.Count > 70);

                InvokeBrushMethod(manager, "RenderPreviewOnNextFrame", null!, EventArgs.Empty);

                Assert.False(GetPrivateField<bool>(manager, "_previewRenderPending"));
                Assert.True(Assert.IsAssignableFrom<Drawing>(stroke.Drawing).Bounds.Width > initialDrawingBounds.Width);
                Assert.False(stroke.Pen.IsFrozen);

                InvokeBrushMethod(manager, "OnEndDraw", new Point(255, 30), CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent));

                Assert.Null(GetPrivateField<DVBrushStroke?>(manager, "_currentStroke"));
                Assert.Single(drawCanvas.UndoStack);
                Assert.Equal(1, addCount);
                Assert.Equal(0, removeCount);
            }
            finally
            {
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void DisposingBrushWithPendingPreviewCancelsTheCompositionCallback()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);

            try
            {
                InvokeBrushMethod(manager, "OnBeginDraw", new Point(10, 30), CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent));
                DVBrushStroke stroke = GetCurrentStroke(manager);
                InvokeBrushMethod(manager, "OnUpdateDraw", new Point(40, 30), CreateMouseMoveArgs());
                Assert.True(GetPrivateField<bool>(manager, "_previewRenderPending"));

                manager.Dispose();

                Assert.False(GetPrivateField<bool>(manager, "_previewRenderPending"));
                Assert.Null(GetPrivateField<DVBrushStroke?>(manager, "_currentStroke"));
                Assert.False(drawCanvas.ContainsVisual(stroke));
                InvokeBrushMethod(manager, "RenderPreviewOnNextFrame", null!, EventArgs.Empty);
                Assert.False(drawCanvas.ContainsVisual(stroke));
            }
            finally
            {
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void ClearingCanvasCancelsBrushPreviewWithoutResurrectingTheStroke()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);

            try
            {
                InvokeBrushMethod(manager, "OnBeginDraw", new Point(10, 30), CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent));
                InvokeBrushMethod(manager, "OnUpdateDraw", new Point(30, 30), CreateMouseMoveArgs());
                Assert.True(GetPrivateField<bool>(manager, "_previewRenderPending"));

                drawCanvas.Clear();
                InvokeBrushMethod(manager, "OnUpdateDraw", new Point(50, 30), CreateMouseMoveArgs());
                InvokeBrushMethod(manager, "OnEndDraw", new Point(50, 30), CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent));

                Assert.Null(GetPrivateField<DVBrushStroke?>(manager, "_currentStroke"));
                Assert.False(GetPrivateField<bool>(manager, "_previewRenderPending"));
                Assert.Empty(drawCanvas.Visuals.OfType<DVBrushStroke>());
                Assert.Empty(drawCanvas.UndoStack);
            }
            finally
            {
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrushCompletionRemovedByVisualsAddLeavesNoDetachedSelectionOrHistory(bool useRemovalCommand)
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);

            try
            {
                InvokeBrushMethod(manager, "OnBeginDraw", new Point(10, 30), CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent));
                InvokeBrushMethod(manager, "OnUpdateDraw", new Point(60, 35), CreateMouseMoveArgs());
                drawCanvas.VisualsAdd += RejectCompletedStroke;

                InvokeBrushMethod(manager, "OnEndDraw", new Point(90, 40), CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent));

                Assert.Null(GetPrivateField<DVBrushStroke?>(manager, "_currentStroke"));
                Assert.Empty(drawCanvas.Visuals.OfType<DVBrushStroke>());
                Assert.Empty(selection.SelectVisuals);
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);

                drawCanvas.VisualsAdd -= RejectCompletedStroke;

                void RejectCompletedStroke(object? sender, VisualChangedEventArgs e)
                {
                    if (e.Visual is not DVBrushStroke stroke)
                        return;

                    if (useRemovalCommand)
                        drawCanvas.RemoveVisualCommand(stroke);
                    else
                        drawCanvas.RemoveVisual(stroke);
                }
            }
            finally
            {
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void BrushCompletionInterruptedByToolSwitchKeepsCanvasMirrorAndHistoryConsistent()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);
            drawCanvas.VisualsAdd += TrackAddedVisual;
            drawCanvas.VisualsRemove += TrackRemovedVisual;
            drawCanvas.VisualsAdd += SwitchTool;

            try
            {
                manager.IsChecked = true;
                InvokeBrushMethod(manager, "OnBeginDraw", new Point(10, 30), CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent));
                InvokeBrushMethod(manager, "OnUpdateDraw", new Point(60, 35), CreateMouseMoveArgs());

                InvokeBrushMethod(manager, "OnEndDraw", new Point(90, 40), CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent));

                Assert.False(manager.IsChecked);
                Assert.Null(context.DrawEditorManager.Current);
                Assert.Empty(drawCanvas.Visuals.OfType<DVBrushStroke>());
                Assert.Empty(context.DrawingVisualLists.OfType<DVBrushStroke>());
                Assert.Empty(selection.SelectVisuals);
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);
            }
            finally
            {
                drawCanvas.VisualsAdd -= TrackAddedVisual;
                drawCanvas.VisualsRemove -= TrackRemovedVisual;
                drawCanvas.VisualsAdd -= SwitchTool;
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }

            void TrackAddedVisual(object? sender, VisualChangedEventArgs e)
            {
                if (e.Visual is IDrawingVisual visual)
                    context.DrawingVisualLists.Add(visual);
            }

            void TrackRemovedVisual(object? sender, VisualChangedEventArgs e)
            {
                if (e.Visual is IDrawingVisual visual)
                    context.DrawingVisualLists.Remove(visual);
            }

            void SwitchTool(object? sender, VisualChangedEventArgs e)
            {
                if (e.Visual is DVBrushStroke)
                    manager.IsChecked = false;
            }
        });
    }

    [Fact]
    public void ArrowToolBuildsPortableLineGeometry()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            Type managerType = typeof(LineManager).Assembly.GetType("ColorVision.ImageEditor.Draw.ArrowManager", throwOnError: true)!;
            using IDisposable manager = Assert.IsAssignableFrom<IDisposable>(Activator.CreateInstance(managerType, context));
            MethodInfo completeArrow = managerType.GetMethod("OnVisualMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;

            DVLine arrow = new(new LineProperties
            {
                Pen = new Pen(Brushes.Red, 2),
                Points = new List<Point> { new(10, 20), new(110, 20) },
            });
            completeArrow.Invoke(manager, new object[] { arrow, new Point(110, 20) });

            Assert.Equal(5, arrow.Points.Count);
            Assert.Equal(new Point(10, 20), arrow.Points[0]);
            Assert.Equal(new Point(110, 20), arrow.Points[1]);
            Assert.Equal(new Point(110, 20), arrow.Points[^1]);
            Assert.True(arrow.Points[2].Y > 20);
            Assert.True(arrow.Points[3].Y < 20);

            AnnotationItem item = Assert.IsType<LineAnnotationItem>(AnnotationMapper.ToItem(arrow));
            AnnotationDocument document = new();
            document.Items.Add(item);
            AnnotationDocument restoredDocument = AnnotationMapper.Deserialize(AnnotationMapper.Serialize(document));
            DVLine restored = Assert.IsType<DVLine>(AnnotationMapper.ToVisual(Assert.Single(restoredDocument.Items)));
            Assert.Equal(arrow.Points, restored.Points);
            Assert.Equal(arrow.Pen.Thickness, restored.Pen.Thickness);
            Assert.Equal(arrow.Pen.Brush.ToString(), restored.Pen.Brush.ToString());

            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void ZeroLengthArrowDoesNotLeaveVisualOrCreationHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            Type managerType = typeof(LineManager).Assembly.GetType("ColorVision.ImageEditor.Draw.ArrowManager", throwOnError: true)!;
            using IDisposable manager = Assert.IsAssignableFrom<IDisposable>(Activator.CreateInstance(managerType, context));
            MethodInfo completeArrow = managerType.GetMethod("OnVisualMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo activeVisualField = typeof(MultiPointDrawingToolBase<DVLine>).GetField("<ActiveVisual>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo creationCommandField = typeof(MultiPointDrawingToolBase<DVLine>).GetField("_activeCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
            DVLine arrow = new(new LineProperties
            {
                Pen = new Pen(Brushes.Red, 2),
                Points = new List<Point> { new(20, 20), new(20, 20) },
            });
            drawCanvas.AddVisualCommand(arrow);
            activeVisualField.SetValue(manager, arrow);
            creationCommandField.SetValue(manager, drawCanvas.UndoStack[^1]);

            completeArrow.Invoke(manager, new object[] { arrow, new Point(20, 20) });

            Assert.False(drawCanvas.ContainsVisual(arrow));
            Assert.Empty(drawCanvas.UndoStack);
            Assert.Null(activeVisualField.GetValue(manager));
            Assert.Null(creationCommandField.GetValue(manager));
            Assert.Empty(selection.SelectVisuals);
            selection.Dispose();
            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void CancellingActiveMarkerPreservesCommandsAddedByRemovalSubscribers()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            using PolygonManager manager = new(context);
            manager.IsChecked = true;
            DVPolygon polygon = new();
            polygon.Points.Add(new Point(10, 10));
            polygon.Points.Add(new Point(20, 20));
            drawCanvas.AddVisualCommand(polygon);
            SetActiveVisual(manager, polygon, drawCanvas.UndoStack[^1]);
            ActionCommand subscriberCommand = new(() => { }, () => { });
            drawCanvas.VisualsRemove += (_, e) =>
            {
                if (ReferenceEquals(e.Visual, polygon))
                    drawCanvas.AddActionCommand(subscriberCommand);
            };

            manager.IsChecked = false;

            Assert.False(drawCanvas.ContainsVisual(polygon));
            Assert.Same(subscriberCommand, Assert.Single(drawCanvas.UndoStack));
            Assert.Empty(drawCanvas.RedoStack);
            selection.Dispose();
            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void UndoingAnActiveMarkerClosesTheToolWithoutLeavingARedoGhost()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            using PolygonManager manager = new(context);
            manager.IsChecked = true;
            ActionCommand unrelatedCommand = new(() => { }, () => { });
            drawCanvas.AddActionCommand(unrelatedCommand);
            DVPolygon polygon = new();
            polygon.Points.Add(new Point(10, 10));
            polygon.Points.Add(new Point(20, 20));
            drawCanvas.AddVisualCommand(polygon);
            SetActiveVisual(manager, polygon, drawCanvas.UndoStack[^1]);
            drawCanvas.VisualsRemove += (_, e) =>
            {
                if (ReferenceEquals(e.Visual, polygon))
                    drawCanvas.Undo();
            };

            drawCanvas.Undo();

            Assert.False(drawCanvas.ContainsVisual(polygon));
            Assert.Same(unrelatedCommand, Assert.Single(drawCanvas.UndoStack));
            Assert.Empty(drawCanvas.RedoStack);
            FieldInfo activeVisualField = typeof(MultiPointDrawingToolBase<DVPolygon>).GetField("<ActiveVisual>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo creationCommandField = typeof(MultiPointDrawingToolBase<DVPolygon>).GetField("_activeCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Null(activeVisualField.GetValue(manager));
            Assert.Null(creationCommandField.GetValue(manager));
            selection.Dispose();
            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void DeletingAnActiveMarkerKeepsCreationAndRemovalHistoryLinear()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            using PolygonManager manager = new(context);
            manager.IsChecked = true;
            DVPolygon polygon = new();
            polygon.Points.Add(new Point(10, 10));
            polygon.Points.Add(new Point(20, 20));
            drawCanvas.AddVisualCommand(polygon);
            SetActiveVisual(manager, polygon, drawCanvas.UndoStack[^1]);

            drawCanvas.RemoveVisualCommand(polygon);

            Assert.False(drawCanvas.ContainsVisual(polygon));
            Assert.Equal(2, drawCanvas.UndoStack.Count);
            drawCanvas.Undo();
            Assert.True(drawCanvas.ContainsVisual(polygon));
            drawCanvas.Undo();
            Assert.False(drawCanvas.ContainsVisual(polygon));
            drawCanvas.Redo();
            Assert.True(drawCanvas.ContainsVisual(polygon));
            drawCanvas.Redo();
            Assert.False(drawCanvas.ContainsVisual(polygon));
            selection.Dispose();
            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void ClearingHistoryInsideAnUndoDoesNotReinsertTheExecutingCommand()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            drawCanvas.AddActionCommand(new ActionCommand(drawCanvas.ClearActionCommand, () => { }));

            drawCanvas.Undo();

            Assert.Empty(drawCanvas.UndoStack);
            Assert.Empty(drawCanvas.RedoStack);
            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void HistoryCollectionCallbacksCannotNestUndoOrRedo()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            int firstUndoCount = 0;
            int secondUndoCount = 0;
            int secondRedoCount = 0;
            ActionCommand first = new(() => firstUndoCount++, () => { });
            ActionCommand second = new(() => secondUndoCount++, () => secondRedoCount++);
            drawCanvas.AddActionCommand(first);
            drawCanvas.AddActionCommand(second);
            drawCanvas.UndoStack.CollectionChanged += (_, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                    drawCanvas.Undo();
            };
            drawCanvas.RedoStack.CollectionChanged += (_, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    drawCanvas.Redo();
            };

            drawCanvas.Undo();

            Assert.Equal(0, firstUndoCount);
            Assert.Equal(1, secondUndoCount);
            Assert.Equal(0, secondRedoCount);
            Assert.Same(first, Assert.Single(drawCanvas.UndoStack));
            Assert.Same(second, Assert.Single(drawCanvas.RedoStack));
            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void DiscardingExecutingCommandFromDestinationCallbackLeavesNoHistoryEntry()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            ActionCommand command = new(() => { }, () => { });
            drawCanvas.AddActionCommand(command);
            MethodInfo discard = typeof(DrawCanvas).GetMethod("DiscardActionCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
            drawCanvas.RedoStack.CollectionChanged += (_, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    discard.Invoke(drawCanvas, new object[] { command });
            };

            drawCanvas.Undo();

            Assert.Empty(drawCanvas.UndoStack);
            Assert.Empty(drawCanvas.RedoStack);
            drawCanvas.Dispose();
        });
    }

    private static void SetActiveVisual(PolygonManager manager, DVPolygon polygon, ActionCommand creationCommand)
    {
        FieldInfo activeVisualField = typeof(MultiPointDrawingToolBase<DVPolygon>).GetField("<ActiveVisual>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo creationCommandField = typeof(MultiPointDrawingToolBase<DVPolygon>).GetField("_activeCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
        activeVisualField.SetValue(manager, polygon);
        creationCommandField.SetValue(manager, creationCommand);
    }

    private static void BeginPolygonGesture(PolygonManager manager, DrawCanvas drawCanvas)
    {
        MethodInfo method = typeof(MultiPointDrawingToolBase<DVPolygon>).GetMethod("HandlePreviewMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(manager, new object[] { drawCanvas, CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent) });
    }

    private static void BeginDragGesture(DragDrawingToolBase manager, DrawCanvas drawCanvas)
    {
        MethodInfo method = typeof(DragDrawingToolBase).GetMethod("HandlePreviewMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(manager, new object[] { drawCanvas, CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent) });
    }

    private static DVPolygon? GetActivePolygon(PolygonManager manager)
    {
        FieldInfo field = typeof(MultiPointDrawingToolBase<DVPolygon>).GetField("<ActiveVisual>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (DVPolygon?)field.GetValue(manager);
    }

    private static void InvokeBrushMethod(BrushManager manager, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(BrushManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(BrushManager).FullName, methodName);
        method.Invoke(manager, arguments);
    }

    private static DVBrushStroke GetCurrentStroke(BrushManager manager)
    {
        return GetPrivateField<DVBrushStroke?>(manager, "_currentStroke")
            ?? throw new InvalidOperationException("Brush manager did not create a stroke.");
    }

    private static T GetPrivateField<T>(BrushManager manager, string fieldName)
    {
        FieldInfo field = typeof(BrushManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(BrushManager).FullName, fieldName);
        return (T)field.GetValue(manager)!;
    }

    private sealed class CountingDragTool : DragDrawingToolBase
    {
        internal CountingDragTool(DrawEditorContext context)
            : base(context)
        {
        }

        internal int EndDrawCount { get; private set; }

        protected override void OnBeginDraw(Point startPoint, MouseButtonEventArgs e)
        {
        }

        protected override void OnUpdateDraw(Point currentPoint, MouseEventArgs e)
        {
        }

        protected override void OnEndDraw(Point endPoint, MouseButtonEventArgs e)
        {
            EndDrawCount++;
        }
    }

    private static MouseButtonEventArgs CreateMouseButtonArgs(RoutedEvent routedEvent, MouseButton button = MouseButton.Left)
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
        {
            RoutedEvent = routedEvent,
        };
    }

    private static MouseEventArgs CreateMouseMoveArgs()
    {
        return new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
        {
            RoutedEvent = Mouse.MouseMoveEvent,
        };
    }

    private sealed class CountingBrushStroke : DVBrushStroke
    {
        public CountingBrushStroke(BrushStrokeProperties properties)
            : base(properties)
        {
        }

        public int RenderCount { get; private set; }

        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }
}
