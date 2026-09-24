using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Solution.MultiImageViewer;
using System.ComponentModel;

namespace ColorVision.UI.Tests;

public sealed class InternalLayoutPropertyVisibilityTests
{
    public static TheoryData<Type> ViewConfigTypes => new()
    {
        typeof(ViewAlgorithmConfig),
        typeof(ViewCalibrationConfig),
        typeof(ViewCameraConfig),
        typeof(ViewSMUConfig),
        typeof(ViewSpectrumConfig),
    };

    [Theory]
    [MemberData(nameof(ViewConfigTypes))]
    public void ViewListHeight_IsNotUserEditable(Type configType)
    {
        PropertyDescriptor height = TypeDescriptor.GetProperties(configType)["Height"]!;

        Assert.False(height.IsBrowsable);
    }

    [Fact]
    public void MultiImageListHeight_IsNotUserEditable()
    {
        PropertyDescriptor height = TypeDescriptor.GetProperties(typeof(MultiImageViewerConfig))[nameof(MultiImageViewerConfig.ListHeight)]!;

        Assert.False(height.IsBrowsable);
    }

    [Theory]
    [InlineData(nameof(WindowConfig.Width))]
    [InlineData(nameof(WindowConfig.Height))]
    [InlineData(nameof(WindowConfig.Left))]
    [InlineData(nameof(WindowConfig.Top))]
    [InlineData(nameof(WindowConfig.WindowStates))]
    [InlineData(nameof(WindowConfig.ScreenDeviceName))]
    public void PersistedWindowPlacement_IsNotUserEditable(string propertyName)
    {
        PropertyDescriptor property = TypeDescriptor.GetProperties(typeof(TestWindowConfig))[propertyName]!;

        Assert.False(property.IsBrowsable);
    }

    [Fact]
    public void WindowRestoreChoice_RemainsUserEditable()
    {
        PropertyDescriptor property = TypeDescriptor.GetProperties(typeof(TestWindowConfig))[nameof(WindowConfig.IsRestoreWindow)]!;

        Assert.True(property.IsBrowsable);
    }

    private sealed class TestWindowConfig : WindowConfig
    {
    }
}
