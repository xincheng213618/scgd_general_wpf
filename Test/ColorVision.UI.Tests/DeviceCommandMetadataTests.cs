using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.LightingController;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class DeviceCommandMetadataTests
{
    public static TheoryData<Type> DeviceTypes => new()
    {
        typeof(DeviceCamera), typeof(DevicePG), typeof(DeviceSMU), typeof(DeviceSpectrum), typeof(DeviceCalibration),
        typeof(DeviceAlgorithm), typeof(DeviceCfwPort), typeof(DeviceSensor), typeof(DeviceLightingController), typeof(DeviceThirdPartyAlgorithms)
    };

    [Theory]
    [MemberData(nameof(DeviceTypes))]
    public void DeviceCommands_HaveLocalizedCategoriesAndLabelsWithoutConstructingDevices(Type type)
    {
        var resources = ColorVision.Engine.Properties.Resources.ResourceManager;
        var culture = CultureInfo.GetCultureInfo("zh-CN");
        var commands = type.GetProperties().Where(property => property.GetCustomAttribute<CommandDisplayAttribute>() != null
            && (property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)).ToArray();
        Assert.NotEmpty(commands);
        foreach (var command in commands)
        {
            var category = Assert.IsType<CategoryAttribute>(command.GetCustomAttribute<CategoryAttribute>());
            Assert.False(string.IsNullOrWhiteSpace(resources.GetString(category.Category, culture)));
            string name = command.GetCustomAttribute<CommandDisplayAttribute>()!.DisplayName;
            string label = resources.GetString(name, culture) ?? name;
            Assert.Contains(label, character => character >= '\u4e00' && character <= '\u9fff');
        }
    }

    [Fact]
    public void SpectrumRefresh_UsesOrdinaryCommandMetadata()
    {
        var command = typeof(DeviceSpectrum).GetProperty(nameof(DeviceSpectrum.RefreshDeviceIdCommand))!;
        Assert.Equal("RefreshDeviceList", command.GetCustomAttribute<CommandDisplayAttribute>()!.DisplayName);
        Assert.Equal("DeviceConnection", command.GetCustomAttribute<CategoryAttribute>()!.Category);
    }
}
