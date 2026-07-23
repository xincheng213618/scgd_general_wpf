using ColorVision.Update;
using System.Diagnostics;

namespace ColorVision.UI.Tests;

public class UpdateDownloadProxyTests
{
    [Fact]
    public void ConfigureChildProcessProxyEnvironment_RemovesInheritedProxyVariablesForDirectMode()
    {
        ProcessStartInfo startInfo = new();
        startInfo.Environment["ALL_PROXY"] = "socks5h://127.0.0.1:10808";
        startInfo.Environment["HTTP_PROXY"] = "http://127.0.0.1:10809";
        startInfo.Environment["HTTPS_PROXY"] = "http://127.0.0.1:10809";
        startInfo.Environment["FTP_PROXY"] = "http://127.0.0.1:10809";
        startInfo.Environment["COLORVISION_PROXY_TEST"] = "keep";

        UpdateNetworkConfig.ConfigureChildProcessProxyEnvironment(startInfo, disableSystemProxy: true);

        Assert.DoesNotContain(startInfo.Environment.Keys, key => key.Equals("ALL_PROXY", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(startInfo.Environment.Keys, key => key.Equals("HTTP_PROXY", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(startInfo.Environment.Keys, key => key.Equals("HTTPS_PROXY", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(startInfo.Environment.Keys, key => key.Equals("FTP_PROXY", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("keep", startInfo.Environment["COLORVISION_PROXY_TEST"]);
    }

    [Fact]
    public void ConfigureChildProcessProxyEnvironment_PreservesProxyVariablesWhenProxyIsEnabled()
    {
        ProcessStartInfo startInfo = new();
        startInfo.Environment["ALL_PROXY"] = "http://127.0.0.1:10809";

        UpdateNetworkConfig.ConfigureChildProcessProxyEnvironment(startInfo, disableSystemProxy: false);

        Assert.Equal("http://127.0.0.1:10809", startInfo.Environment["ALL_PROXY"]);
    }
}
