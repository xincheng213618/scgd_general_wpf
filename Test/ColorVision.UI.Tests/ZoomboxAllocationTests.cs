using ColorVision.ImageEditor;

namespace ColorVision.UI.Tests;

public sealed class ZoomboxAllocationTests
{
    [Fact]
    public void MatrixChangeNotificationsReuseEventArgsEmpty()
    {
        StaTest.Run(() =>
        {
            Zoombox zoombox = new();
            EventArgs? received = null;
            zoombox.ContentMatrixChanged += (_, args) => received = args;

            zoombox.ZoomNone();

            Assert.Same(EventArgs.Empty, received);
        });
    }
}
