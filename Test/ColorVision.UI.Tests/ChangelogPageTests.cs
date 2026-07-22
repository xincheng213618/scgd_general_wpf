using ColorVision.Update;

namespace ColorVision.UI.Tests;

public class ChangelogPageTests
{
    [Fact]
    public void UrlUsesPublicChangelogPage()
    {
        Assert.Equal("http://xc213618.ddns.me:9998/changelog", ChangelogPage.Url);
    }
}
