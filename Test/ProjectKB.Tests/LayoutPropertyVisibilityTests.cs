using System.ComponentModel;
using Xunit;

namespace ProjectKB.Tests;

public sealed class LayoutPropertyVisibilityTests
{
    [Fact]
    public void ResultPaneHeight_IsNotUserEditable()
    {
        PropertyDescriptor height = TypeDescriptor.GetProperties(typeof(ViewResultManagerConfig))[nameof(ViewResultManagerConfig.Height)]!;

        Assert.False(height.IsBrowsable);
    }
}
