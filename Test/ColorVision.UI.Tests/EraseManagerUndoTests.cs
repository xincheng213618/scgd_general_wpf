using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class EraseManagerUndoTests
{
    [Fact]
    public void ClearingCanvasDoesNotPublishRemovalForOverlayVisuals()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas drawCanvas = new();
            DrawingVisual overlay = new();
            int removalCount = 0;
            List<VisualChangeType> changeTypes = new();
            drawCanvas.VisualsRemove += (_, _) => removalCount++;
            drawCanvas.VisualsChanged += (_, e) => changeTypes.Add(e.ChangeType);
            drawCanvas.AddOverlayVisual(overlay);
            Assert.True(drawCanvas.ContainsVisual(overlay));

            drawCanvas.Clear();

            Assert.Equal(0, removalCount);
            Assert.False(drawCanvas.ContainsVisual(overlay));
            Assert.Equal(new[] { VisualChangeType.Clear }, changeTypes);
            drawCanvas.Dispose();
        });
    }

    [Fact]
    public void TemporaryMarqueeDoesNotEnterUndoHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            using EraseFixture fixture = new();
            int initialVisualCount = fixture.Canvas.Visuals.Count;

            InvokePrivate(fixture.Manager, "BeginErase", new Point(150, 150));

            Assert.Empty(fixture.Canvas.UndoStack);
            Assert.Equal(initialVisualCount + 1, fixture.Canvas.Visuals.Count);

            InvokePrivate(fixture.Manager, "CompleteErase", new Point(160, 160));

            Assert.Empty(fixture.Canvas.UndoStack);
            Assert.Equal(initialVisualCount, fixture.Canvas.Visuals.Count);
        });
    }

    [Fact]
    public void RightMouseUpDoesNotCompleteOrConsumeAnEraseGesture()
    {
        WpfTestHost.Invoke(() =>
        {
            using EraseFixture fixture = new();
            InvokePrivate(fixture.Manager, "BeginErase", new Point(40, 50));
            MouseButtonEventArgs rightMouseUp = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = Mouse.PreviewMouseUpEvent,
            };

            InvokePrivate(fixture.Manager, "Image_PreviewMouseUp", fixture.Canvas, rightMouseUp);

            Assert.False(rightMouseUp.Handled);
            Assert.True(GetInstanceField<bool>(fixture.Manager, "IsMouseDown"));
            Assert.Empty(fixture.Canvas.UndoStack);

            InvokePrivate(fixture.Manager, "CompleteErase", new Point(40, 50));
        });
    }

    [Fact]
    public void ErasedVisualsShareOneUndoTransaction()
    {
        WpfTestHost.Invoke(() =>
        {
            using EraseFixture fixture = new();
            DrawingVisual first = CreateRectangle(new Rect(10, 10, 20, 20));
            DrawingVisual second = CreateRectangle(new Rect(50, 10, 20, 20));
            fixture.Canvas.AddVisual(first);
            fixture.Canvas.AddVisual(second);

            InvokePrivate(fixture.Manager, "RemoveVisualsAsSingleCommand", new HashSet<Visual> { first, second });

            Assert.Single(fixture.Canvas.UndoStack);
            Assert.False(fixture.Canvas.ContainsVisual(first));
            Assert.False(fixture.Canvas.ContainsVisual(second));

            fixture.Canvas.Undo();

            Assert.Equal(new Visual[] { fixture.Selection, first, second }, fixture.Canvas.Visuals);

            fixture.Canvas.Redo();

            Assert.False(fixture.Canvas.ContainsVisual(first));
            Assert.False(fixture.Canvas.ContainsVisual(second));
        });
    }

    [Fact]
    public void MarqueeResourcesAreCachedAndFrozen()
    {
        SolidColorBrush fill = Assert.IsType<SolidColorBrush>(GetStaticField("SelectionFill"));
        Pen border = Assert.IsType<Pen>(GetStaticField("SelectionBorder"));

        Assert.True(fill.IsFrozen);
        Assert.Equal(Color.FromArgb(0x77, 0xF3, 0xF3, 0xF3), fill.Color);
        Assert.True(border.IsFrozen);
        Assert.Same(Brushes.Blue, border.Brush);
        Assert.Equal(1, border.Thickness);
    }

    private static DrawingVisual CreateRectangle(Rect rect)
    {
        DrawingVisual visual = new();
        using DrawingContext context = visual.RenderOpen();
        context.DrawRectangle(Brushes.Red, null, rect);
        return visual;
    }

    private static void InvokePrivate(EraseManager manager, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(EraseManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(EraseManager).FullName, methodName);
        method.Invoke(manager, arguments);
    }

    private static T GetInstanceField<T>(EraseManager manager, string fieldName)
    {
        FieldInfo field = typeof(EraseManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(EraseManager).FullName, fieldName);
        return (T)field.GetValue(manager)!;
    }

    private static object GetStaticField(string fieldName)
    {
        FieldInfo field = typeof(EraseManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(EraseManager).FullName, fieldName);
        return field.GetValue(null) ?? throw new InvalidOperationException($"Field '{fieldName}' is null.");
    }

    private sealed class EraseFixture : IDisposable
    {
        internal EraseFixture()
        {
            Canvas = new DrawCanvas { Width = 200, Height = 200 };
            Zoombox = new Zoombox { Child = Canvas, ContentMatrix = Matrix.Identity };
            Context = new DrawEditorContext(Canvas, Zoombox);
            Selection = new SelectEditorVisual(Context);
            Context.SelectionVisual = Selection;
            Manager = new EraseManager(Context);
            Manager.IsChecked = true;
        }

        internal DrawCanvas Canvas { get; }
        internal Zoombox Zoombox { get; }
        internal DrawEditorContext Context { get; }
        internal SelectEditorVisual Selection { get; }
        internal EraseManager Manager { get; }

        public void Dispose()
        {
            Manager.Dispose();
            Selection.Dispose();
            Canvas.Dispose();
        }
    }
}
