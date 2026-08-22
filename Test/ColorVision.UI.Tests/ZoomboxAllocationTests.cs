using ColorVision.ImageEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public sealed class ZoomboxAllocationTests
{
    [Fact]
    public void MatrixChangeNotificationsReuseEventArgsEmpty()
    {
        RunOnStaThread(() =>
        {
            Zoombox zoombox = new();
            EventArgs? received = null;
            zoombox.ContentMatrixChanged += (_, args) => received = args;

            zoombox.ZoomNone();

            Assert.Same(EventArgs.Empty, received);
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
