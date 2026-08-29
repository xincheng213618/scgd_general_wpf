using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Annotations;
using ColorVision.ImageEditor.Draw.Special;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class DrawMarkerToolTests
{
    [Fact]
    public void TransientCrosshairAndMagnifierDoNotCreateUndoHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            Crosshair crosshair = new(context);
            MouseMagnifierManager magnifier = new(context);

            try
            {
                crosshair.IsShow = true;
                magnifier.IsChecked = true;
                magnifier.MouseLeave(drawCanvas, CreateMouseMoveArgs());
                magnifier.MouseEnter(drawCanvas, CreateMouseMoveArgs());

                Assert.Equal(2, drawCanvas.Visuals.Count);
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);

                crosshair.IsShow = false;
                magnifier.IsChecked = false;

                Assert.Empty(drawCanvas.Visuals);
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);
            }
            finally
            {
                crosshair.IsShow = false;
                magnifier.IsChecked = false;
                drawCanvas.Dispose();
            }
        });
    }

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
    public void MultiPointToolLosingMouseCaptureCancelsTheActiveMarker()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            Grid root = new();
            root.Children.Add(zoombox);
            Button captureTarget = new()
            {
                Width = 1,
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            root.Children.Add(captureTarget);
            Window window = new()
            {
                Content = root,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 400,
                Height = 300,
                Left = -10000,
                Top = -10000,
            };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            LineManager manager = new(context);

            try
            {
                window.Show();
                manager.IsChecked = true;
                typeof(MultiPointDrawingToolBase<DVLine>)
                    .GetMethod("HandlePreviewMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(manager, [drawCanvas, CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent)]);

                Assert.True(drawCanvas.IsMouseCaptured);
                Assert.Single(drawCanvas.Visuals.OfType<DVLine>());
                Assert.Single(drawCanvas.UndoStack);
                Assert.True(captureTarget.CaptureMouse());

                Assert.False(manager.IsChecked);
                Assert.Null(context.DrawEditorManager.Current);
                Assert.Empty(drawCanvas.Visuals.OfType<DVLine>());
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);
                Assert.Empty(selection.SelectVisuals);
            }
            finally
            {
                captureTarget.ReleaseMouseCapture();
                window.Close();
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void LosingMouseCapturePreservesAnActiveMultiClickDraft()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            Grid root = new();
            root.Children.Add(zoombox);
            Button captureTarget = new()
            {
                Width = 1,
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            root.Children.Add(captureTarget);
            Window window = new()
            {
                Content = root,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 400,
                Height = 300,
                Left = -10000,
                Top = -10000,
            };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            PolygonManager manager = new(context);

            try
            {
                window.Show();
                manager.IsChecked = true;
                BeginPolygonGesture(manager, drawCanvas);
                DVPolygon draft = Assert.Single(drawCanvas.Visuals.OfType<DVPolygon>());
                Assert.True(drawCanvas.IsMouseCaptured);

                Assert.True(captureTarget.CaptureMouse());

                Assert.True(manager.IsChecked);
                Assert.Same(manager, context.DrawEditorManager.Current);
                Assert.Same(draft, GetActivePolygon(manager));
                Assert.True(drawCanvas.ContainsVisual(draft));
                Assert.Single(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);

                manager.IsChecked = false;
                Assert.False(drawCanvas.ContainsVisual(draft));
                Assert.Empty(drawCanvas.UndoStack);
            }
            finally
            {
                captureTarget.ReleaseMouseCapture();
                window.Close();
                manager.Dispose();
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void IncompletePolygonAndBezierCompletionLeavesNoGhostOrHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            using PolygonManager polygonManager = new(context);
            using BezierCurveManager bezierManager = new(context);

            DVPolygon polygon = new(new PolygonProperties
            {
                Points = [new Point(20, 20), new Point(20, 20)],
            });
            AssertIncompleteCompletionIsCancelled(polygonManager, polygon, drawCanvas);

            DVBezierCurve bezier = new(new BezierCurveProperties
            {
                Points = [new Point(40, 40), new Point(40, 40)],
            })
            {
                AutoAttributeChanged = false,
            };
            AssertIncompleteCompletionIsCancelled(bezierManager, bezier, drawCanvas);

            Assert.Empty(selection.SelectVisuals);
            selection.Dispose();
            drawCanvas.Dispose();
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

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void BrushNumericSettingsRejectNonFiniteValues(double invalidValue)
    {
        BrushManagerConfig config = new()
        {
            StrokeThickness = 7,
            SampleSpacing = 3,
        };
        BrushStrokeProperties properties = new()
        {
            StrokeThickness = 9,
        };

        config.StrokeThickness = invalidValue;
        config.SampleSpacing = invalidValue;
        properties.ScreenThickness = invalidValue;
        properties.StrokeThickness = invalidValue;

        Assert.Equal(7, config.StrokeThickness);
        Assert.Equal(3, config.SampleSpacing);
        Assert.Equal(9, properties.ScreenThickness);
        Assert.Equal(9, properties.Pen.Thickness);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void MultiPointNumericSettingsRejectNonFiniteThickness(double invalidValue)
    {
        MultiPointDrawingToolStyleConfig config = new() { StrokeThickness = 5 };
        LineProperties line = new() { StrokeThickness = 6 };
        PolygonProperties polygon = new() { StrokeThickness = 7 };
        BezierCurveProperties bezier = new() { StrokeThickness = 8 };

        config.StrokeThickness = invalidValue;
        line.StrokeThickness = invalidValue;
        polygon.StrokeThickness = invalidValue;
        bezier.StrokeThickness = invalidValue;

        Assert.Equal(5, config.StrokeThickness);
        Assert.Equal(6, line.StrokeThickness);
        Assert.Equal(7, polygon.StrokeThickness);
        Assert.Equal(8, bezier.StrokeThickness);
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
    [InlineData(double.NaN, 4)]
    [InlineData(double.PositiveInfinity, 4)]
    [InlineData(double.MaxValue, 3579.1)]
    public void BrushLayoutScaleNormalizesUnsafePublicScale(double scale, double expectedThickness)
    {
        WpfTestHost.Invoke(() =>
        {
            DVBrushStroke stroke = new(new BrushStrokeProperties
            {
                Pen = new Pen(Brushes.Gold, 4),
                ScreenThickness = 4,
                Points = new List<Point> { new(5, 5), new(30, 25) },
            });

            stroke.ApplyLayoutScale(new DrawingVisualScaleContext(true, scale, 0));
            stroke.Render();

            Assert.True(double.IsFinite(stroke.Pen.Thickness));
            Assert.Equal(expectedThickness, stroke.Pen.Thickness);
        });
    }

    [Theory]
    [InlineData(-2.0)]
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
            double documentSpacing = manager.Config.SampleSpacing / Math.Abs(zoomRatio);

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
    public void ClickingWithBrushCreatesAnUndoableDot()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);
            Point point = new(24, 36);

            try
            {
                manager.IsChecked = true;
                InvokeBrushMethod(manager, "OnBeginDraw", point, CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent));
                InvokeBrushMethod(manager, "OnEndDraw", point, CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent));

                DVBrushStroke stroke = Assert.Single(drawCanvas.Visuals.OfType<DVBrushStroke>());
                Assert.Equal(point, Assert.Single(stroke.Points));
                Assert.Single(drawCanvas.UndoStack);
                Assert.Contains(stroke, selection.SelectVisuals);

                drawCanvas.Undo();
                Assert.False(drawCanvas.ContainsVisual(stroke));
                drawCanvas.Redo();
                Assert.True(drawCanvas.ContainsVisual(stroke));
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
    public void BrushLosingMouseCaptureCancelsTheTransientStroke()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            Grid root = new();
            root.Children.Add(zoombox);
            Button captureTarget = new()
            {
                Width = 1,
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            root.Children.Add(captureTarget);
            Window window = new()
            {
                Content = root,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 400,
                Height = 300,
                Left = -10000,
                Top = -10000,
            };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            BrushManager manager = new(context);

            try
            {
                window.Show();
                manager.IsChecked = true;
                BeginDragGesture(manager, drawCanvas);

                Assert.True(drawCanvas.IsMouseCaptured);
                Assert.Single(drawCanvas.Visuals.OfType<DVBrushStroke>());
                Assert.True(captureTarget.CaptureMouse());

                Assert.False(manager.IsChecked);
                Assert.Empty(drawCanvas.Visuals.OfType<DVBrushStroke>());
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);
                Assert.Empty(selection.SelectVisuals);
            }
            finally
            {
                captureTarget.ReleaseMouseCapture();
                window.Close();
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
    public void ImportedNonFiniteLineThicknessUsesTheFiniteFallback()
    {
        WpfTestHost.Invoke(() =>
        {
            LineAnnotationItem item = new()
            {
                Points = [new AnnotationPoint { X = 10, Y = 20 }, new AnnotationPoint { X = 80, Y = 45 }],
                Style = new AnnotationShapeStyle
                {
                    StrokeColor = Colors.Red.ToString(),
                    StrokeThickness = double.PositiveInfinity,
                },
            };

            DVLine line = Assert.IsType<DVLine>(AnnotationMapper.ToVisual(item));
            line.Render();
            Rect bounds = line.GetRect();

            Assert.Equal(1, line.Pen.Thickness);
            Assert.True(double.IsFinite(bounds.X));
            Assert.True(double.IsFinite(bounds.Y));
            Assert.True(double.IsFinite(bounds.Width));
            Assert.True(double.IsFinite(bounds.Height));
        });
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void MarkerAnnotationImportRejectsNonFiniteCoordinates(double invalidValue)
    {
        WpfTestHost.Invoke(() =>
        {
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => AnnotationMapper.ToProperties(new TextAnnotationItem
            {
                Position = new AnnotationPoint { X = invalidValue, Y = 0 },
            }));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => AnnotationMapper.ToProperties(new LineAnnotationItem
            {
                Points = [new AnnotationPoint { X = 0, Y = 0 }, new AnnotationPoint { X = invalidValue, Y = 1 }],
            }));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => AnnotationMapper.ToProperties(new PolygonAnnotationItem
            {
                Points = [new AnnotationPoint { X = 0, Y = 0 }, new AnnotationPoint { X = 1, Y = invalidValue }],
            }));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => AnnotationMapper.ToProperties(new BezierCurveAnnotationItem
            {
                Points = [new AnnotationPoint { X = 0, Y = 0 }, new AnnotationPoint { X = invalidValue, Y = 1 }],
            }));
        });
    }

    [Fact]
    public void IncompleteFiniteMarkerAnnotationsRemainImportCompatible()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties text = Assert.IsType<TextProperties>(AnnotationMapper.ToProperties(new TextAnnotationItem
            {
                Position = null!,
            }));
            Assert.Equal(new Point(), text.Position);

            LineProperties emptyLine = Assert.IsType<LineProperties>(AnnotationMapper.ToProperties(new LineAnnotationItem
            {
                Points = null!,
            }));
            Assert.Empty(emptyLine.Points);

            LineProperties line = Assert.IsType<LineProperties>(AnnotationMapper.ToProperties(new LineAnnotationItem
            {
                Points = [new AnnotationPoint { X = 4, Y = 5 }],
            }));
            Assert.Equal(new Point(4, 5), Assert.Single(line.Points));

            PolygonProperties openPolygon = Assert.IsType<PolygonProperties>(AnnotationMapper.ToProperties(new PolygonAnnotationItem
            {
                Points = [new AnnotationPoint()],
            }));
            Assert.Equal(new Point(), Assert.Single(openPolygon.Points));

            PolygonProperties closedPolygon = Assert.IsType<PolygonProperties>(AnnotationMapper.ToProperties(new PolygonAnnotationItem
            {
                IsClosed = true,
                Points = [new AnnotationPoint(), new AnnotationPoint { X = 1 }],
            }));
            Assert.Equal([new Point(), new Point(1, 0)], closedPolygon.Points);

            BezierCurveProperties bezier = Assert.IsType<BezierCurveProperties>(AnnotationMapper.ToProperties(new BezierCurveAnnotationItem
            {
                Points = [new AnnotationPoint(), null!],
            }));
            Assert.Equal([new Point(), new Point()], bezier.Points);

            DrawingVisualBase[] source =
            [
                new DVLine(new LineProperties { Points = [new Point(1, 2)] }),
                new DVPolygon(new PolygonProperties { Points = [new Point(3, 4), new Point(5, 6)] }) { IsComple = true },
                new DVBezierCurve(new BezierCurveProperties { Points = [] }),
            ];
            AnnotationDocument document = AnnotationMapper.CreateDocument(source);
            AnnotationDocument deserialized = AnnotationMapper.Deserialize(AnnotationMapper.Serialize(document));
            IReadOnlyList<DrawingVisualBase> imported = AnnotationMapper.ToVisuals(deserialized);

            Assert.Equal(3, imported.Count);
            Assert.Single(Assert.IsType<DVLine>(imported[0]).Points);
            DVPolygon importedPolygon = Assert.IsType<DVPolygon>(imported[1]);
            Assert.Equal(2, importedPolygon.Points.Count);
            Assert.True(importedPolygon.IsComple);
            Assert.Empty(Assert.IsType<DVBezierCurve>(imported[2]).Points);
        });
    }

    [Fact]
    public void LineCreationBeforeZoomInitializationUsesFiniteFallbackPen()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = default };
            DrawEditorContext context = new(drawCanvas, zoombox);
            LineManager manager = new(context);
            DVLine line = new();
            MethodInfo initialize = typeof(LineManager).GetMethod("OnVisualCreated", BindingFlags.Instance | BindingFlags.NonPublic)!;

            try
            {
                initialize.Invoke(manager, [line]);

                Assert.True(double.IsFinite(line.Pen.Thickness));
                Assert.Equal(1, line.Pen.Thickness);
            }
            finally
            {
                manager.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void MultiPointCreationBeforeZoomInitializationUsesFiniteFallbackPens()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = default };
            DrawEditorContext context = new(drawCanvas, zoombox);
            PolygonManager polygonManager = new(context);
            BezierCurveManager bezierManager = new(context);
            DVPolygon polygon = new();
            DVBezierCurve bezier = new();

            try
            {
                typeof(PolygonManager).GetMethod("OnVisualCreated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(polygonManager, [polygon]);
                typeof(BezierCurveManager).GetMethod("OnVisualCreated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bezierManager, [bezier]);

                Assert.True(double.IsFinite(polygon.Pen.Thickness));
                Assert.True(double.IsFinite(bezier.Pen.Thickness));
                Assert.Equal(1, polygon.Pen.Thickness);
                Assert.Equal(1, bezier.Pen.Thickness);
            }
            finally
            {
                polygonManager.Dispose();
                bezierManager.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void ZeroLengthLineDoesNotLeaveVisualOrCreationHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            LineManager manager = new(context);

            try
            {
                manager.IsChecked = true;
                MethodInfo mouseDown = typeof(MultiPointDrawingToolBase<DVLine>).GetMethod("HandlePreviewMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
                MethodInfo mouseUp = typeof(MultiPointDrawingToolBase<DVLine>).GetMethod("HandlePreviewMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;

                mouseDown.Invoke(manager, [drawCanvas, CreateMouseButtonArgs(Mouse.PreviewMouseDownEvent)]);
                mouseUp.Invoke(manager, [drawCanvas, CreateMouseButtonArgs(Mouse.PreviewMouseUpEvent)]);

                Assert.Empty(drawCanvas.Visuals.OfType<DVLine>());
                Assert.Empty(drawCanvas.UndoStack);
                Assert.Empty(drawCanvas.RedoStack);
                Assert.Empty(selection.SelectVisuals);
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

    private static void AssertIncompleteCompletionIsCancelled<TVisual>(
        MultiPointDrawingToolBase<TVisual> manager,
        TVisual visual,
        DrawCanvas drawCanvas)
        where TVisual : DrawingVisual, ISelectVisual
    {
        Type managerBaseType = typeof(MultiPointDrawingToolBase<TVisual>);
        FieldInfo activeVisualField = managerBaseType.GetField("<ActiveVisual>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo creationCommandField = managerBaseType.GetField("_activeCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo complete = managerBaseType.GetMethod("CompleteCurrentVisual", BindingFlags.Instance | BindingFlags.NonPublic)!;
        drawCanvas.AddVisualCommand(visual);
        activeVisualField.SetValue(manager, visual);
        creationCommandField.SetValue(manager, drawCanvas.UndoStack[^1]);

        complete.Invoke(manager, [true]);

        Assert.False(drawCanvas.ContainsVisual(visual));
        Assert.Empty(drawCanvas.UndoStack);
        Assert.Empty(drawCanvas.RedoStack);
        Assert.Null(activeVisualField.GetValue(manager));
        Assert.Null(creationCommandField.GetValue(manager));
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
