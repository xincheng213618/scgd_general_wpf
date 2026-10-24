using ColorVision.Engine.Services.Devices.Spectrum;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class DeviceCommandMetadataTests
{
    [Fact]
    public void SpectrumDriverTool_IsBundledAndUsesDeviceConnectionMetadata()
    {
        var command = typeof(DeviceSpectrum).GetProperty(nameof(DeviceSpectrum.OpenSpectrumDriverToolCommand))!;
        Assert.Equal("SpectrumDriverTool", command.GetCustomAttribute<CommandDisplayAttribute>()!.DisplayName);
        Assert.Equal("DeviceConnection", command.GetCustomAttribute<CategoryAttribute>()!.Category);
        Assert.Equal("SpectrumDriverToolHint", command.GetCustomAttribute<DescriptionAttribute>()!.Description);

        string toolPath = DeviceSpectrum.GetSpectrumDriverToolPath(AppContext.BaseDirectory);
        Assert.True(File.Exists(toolPath), $"Missing bundled Spectrum driver tool: {toolPath}");
        Assert.True(DeviceSpectrum.HasExpectedSpectrumDriverToolHash(toolPath));
    }
}
