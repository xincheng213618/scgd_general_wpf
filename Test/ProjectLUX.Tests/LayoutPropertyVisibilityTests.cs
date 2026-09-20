using System.ComponentModel;
using Xunit;

namespace ProjectLUX.Tests;

public sealed class LayoutPropertyVisibilityTests
{
    [Theory]
    [InlineData(typeof(ViewResultManagerConfig), nameof(ViewResultManagerConfig.Height))]
    [InlineData(typeof(ProjectLUXConfig), nameof(ProjectLUXConfig.Height))]
    public void RuntimeManagedHeights_AreNotUserEditable(Type configType, string propertyName)
    {
        PropertyDescriptor property = TypeDescriptor.GetProperties(configType)[propertyName]!;

        Assert.False(property.IsBrowsable);
    }
}
