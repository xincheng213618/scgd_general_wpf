using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColorVision.UI.Tests;

public sealed class TransientRoiSelectionTests
{
    [Fact]
    public async Task ReplacementCancelsOldSelectionAndRestoresCanvasInteraction()
    {
        var (first, second) = WithCanvas((draw, source) =>
        {
            var first = new TransientRoiSelectionSession(draw, SelectShapeType.Rectangle).Start();
            var second = new TransientRoiSelectionSession(draw, SelectShapeType.Polygon).Start();
            try
            {
                Assert.True(first.IsCompletedSuccessfully);
                Assert.False(second.IsCompleted);
                Assert.Same(Cursors.Cross, draw.Zoombox.Cursor);
                Assert.Equal(ModifierKeys.Control, draw.Zoombox.ActivateOn);
                Assert.False(draw.IsImageEditMode);

                PressKey(draw.DrawCanvas, source, Key.Escape);
                Assert.True(second.IsCompletedSuccessfully);
                Assert.Same(Cursors.Arrow, draw.Zoombox.Cursor);
                Assert.Equal(ModifierKeys.Alt, draw.Zoombox.ActivateOn);
                Assert.True(draw.IsImageEditMode);
                return (first, second);
            }
            finally
            {
                if (!first.IsCompleted) PressKey(draw.DrawCanvas, source, Key.Escape);
                if (!second.IsCompleted) PressKey(draw.DrawCanvas, source, Key.Escape);
            }
        });

        Assert.Null(await first);
        Assert.Null(await second);
    }

    [Theory]
    [InlineData(SelectShapeType.Polygon, 3)]
    [InlineData(SelectShapeType.Quadrilateral, 4)]
    public async Task KeyboardCompletionRequiresTheShapePointCount(SelectShapeType shape, int count)
    {
        var selection = WithCanvas((draw, source) =>
        {
            var session = new TransientRoiSelectionSession(draw, shape);
            var selection = session.Start();
            try
            {
                var field = typeof(TransientRoiSelectionSession).GetField("_polygonPoints", BindingFlags.Instance | BindingFlags.NonPublic);
                var points = Assert.IsType<List<Point>>(field!.GetValue(session));
                points.AddRange([new(2, 2), new(12, 2), new(12, 12)]);
                PressKey(draw.DrawCanvas, source, Key.Enter);
                Assert.Equal(count == 3, selection.IsCompleted);
                if (count == 4)
                {
                    points.Add(new(2, 12));
                    PressKey(draw.DrawCanvas, source, Key.Enter);
                    Assert.True(selection.IsCompletedSuccessfully);
                }
                return selection;
            }
            finally
            {
                if (!selection.IsCompleted) PressKey(draw.DrawCanvas, source, Key.Escape);
            }
        });

        var result = Assert.IsType<SelectResult>(await selection);
        Assert.Equal(shape, result.ShapeType);
        Assert.Equal(count, result.Points.Count);
    }

    private static T WithCanvas<T>(Func<DrawEditorContext, HwndSource, T> action)
    {
        return WpfTestHost.Invoke(() =>
        {
            using var canvas = new DrawCanvas();
            using var source = new HwndSource(new HwndSourceParameters("Transient selection test", 32, 32));
            var zoom = new Zoombox { Child = canvas, Cursor = Cursors.Arrow, ActivateOn = ModifierKeys.Alt };
            source.RootVisual = zoom;
            var draw = new DrawEditorContext(canvas, zoom) { IsImageEditMode = true };
            using var mouseInfo = draw.MouseInfoProvider;
            return action(draw, source);
        });
    }

    private static void PressKey(DrawCanvas canvas, HwndSource source, Key key)
    {
        canvas.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
    }
}
