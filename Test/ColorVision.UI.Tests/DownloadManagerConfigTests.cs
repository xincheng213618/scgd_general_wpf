using ColorVision.UI.Desktop.Download;

namespace ColorVision.UI.Tests;

public class DownloadManagerConfigTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(6800, 6800)]
    [InlineData(99999, 65535)]
    public void RpcPort_IsClampedToValidRange(int input, int expected)
    {
        var config = new DownloadManagerConfig();
        config.RpcPort = input;
        Assert.Equal(expected, config.RpcPort);
    }
}
