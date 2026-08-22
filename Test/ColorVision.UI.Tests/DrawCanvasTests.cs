using ColorVision.ImageEditor;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class DrawCanvasTests
{
    [Fact]
    public void ContainsVisualTracksAddAndRemove()
    {
        RunOnStaThread(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual visual = new();

            Assert.False(canvas.ContainsVisual(visual));

            canvas.AddVisual(visual);
            Assert.True(canvas.ContainsVisual(visual));

            canvas.RemoveVisual(visual);
            Assert.False(canvas.ContainsVisual(visual));
        });
    }

    [Fact]
    public void BatchTopVisualsPreservesExistingOrderingSemantics()
    {
        RunOnStaThread(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual first = new();
            DrawingVisual second = new();
            DrawingVisual third = new();
            canvas.AddVisual(first);
            canvas.AddVisual(second);
            canvas.AddVisual(third);

            canvas.BatchTopVisuals([second, first]);

            Assert.Equal(new Visual[] { third, second, first }, canvas.Visuals);
        });
    }

    [Fact]
    public void UndoRemoveRestoresOriginalVisualOrder()
    {
        RunOnStaThread(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual first = new();
            DrawingVisual second = new();
            DrawingVisual third = new();
            canvas.AddVisual(first);
            canvas.AddVisual(second);
            canvas.AddVisual(third);

            canvas.RemoveVisualCommand(second);
            canvas.Undo();

            Assert.Equal(new Visual[] { first, second, third }, canvas.Visuals);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
