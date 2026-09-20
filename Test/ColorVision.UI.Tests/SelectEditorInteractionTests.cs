using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class SelectEditorInteractionTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void GeometryNotificationsCanReplaceSelectionDuringMove(int count, bool drag)
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new();
            Zoombox zoom = new() { Child = canvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(canvas, zoom) { IsImageEditMode = true };
            using SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            var originals = Enumerable.Range(0, count).Select(i => new DVRectangle(new()
            {
                Rect = new Rect(20 + i * 100, 30, 60, 70),
            })).ToArray();
            DVRectangle replacement = new(new() { Rect = new Rect(300, 30, 60, 70) });
            foreach (var visual in originals.Append(replacement)) canvas.AddVisual(visual);
            selection.SetRenders(originals);
            var before = originals.Select(v => v.GetRect()).ToArray();
            var replacementBefore = replacement.GetRect();
            int notifications = 0;
            originals[0].Attribute.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(RectangleProperties.Rect)) return;
                notifications++;
                selection.SetRender(replacement);
            };

            Vector delta = new(6, 4);
            if (drag)
            {
                MouseEventArgs move = new(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent };
                // Set the gesture state without moving the user's physical pointer.
                typeof(SelectEditorVisual).GetField("IsMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(selection, true);
                typeof(SelectEditorVisual).GetField("LastMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(selection, move.GetPosition(canvas) - delta);
                zoom.Cursor = Cursors.SizeAll;
                canvas.RaiseEvent(move);
            }
            else
            {
                typeof(SelectEditorVisual).GetMethod("TransformSelection", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(selection, [delta.X, delta.Y, 0d, 0d]);
            }

            Assert.Equal(1, notifications);
            for (int i = 0; i < originals.Length; i++)
                Assert.Equal(new Rect(before[i].Location + delta, before[i].Size), originals[i].GetRect());
            Assert.Equal(replacementBefore, replacement.GetRect());
            Assert.Same(replacement, Assert.Single(selection.SelectVisuals));
        });
    }
}
