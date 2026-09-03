using ColorVision.Engine.Services.Devices.Spectrum;
using EngineSpectrometerType = ColorVision.Engine.Services.Devices.Spectrum.Configs.SpectrometerType;

namespace Spectrum.Tests;

public sealed class SpectrumDeviceDiscoveryTests
{
    [Fact]
    public void Discovery_SearchesEveryDriverEvenAfterFindingADevice()
    {
        var calls = new List<(int Type, int Port)>();
        var results = SpectrumDeviceDiscovery.Discover(0, (type, port, buffer, length) =>
        {
            calls.Add((type, port));
            buffer.Append(type == 0 ? "{\"number\":1,\"ID\":[\"CM-001\"]}" : "{\"number\":0,\"ID\":[]}");
            return 1;
        });

        Assert.Equal(new[] { (0, 0), (1, 0), (2, 0) }, calls);
        Assert.Equal("CM-001", Assert.Single(results[0].SerialNumbers));
        Assert.All(results.Skip(1), result => Assert.Empty(result.SerialNumbers));
    }

    [Fact]
    public void Discovery_SearchesUsbAndConfiguredSerialPortButGaolitongOnlyUsesUsb()
    {
        var calls = new List<(int Type, int Port)>();
        SpectrumDeviceDiscovery.Discover(7, (type, port, buffer, length) =>
        {
            calls.Add((type, port));
            buffer.Append("[]");
            return 1;
        });

        Assert.Equal(new[] { (0, 0), (0, 7), (1, 0), (1, 7), (2, 0) }, calls);
    }

    [Fact]
    public void Discovery_ContinuesAfterDriverExceptionAndNativeFailure()
    {
        var results = SpectrumDeviceDiscovery.Discover(0, (type, port, buffer, length) =>
        {
            if (type == 0)
                throw new DllNotFoundException("Missing vendor driver");
            buffer.Append("{\"number\":1,\"ID\":[\"DEVICE-002\"]}");
            return type == 1 ? -7 : 1;
        });

        Assert.Contains("Missing vendor driver", results[0].Error);
        Assert.Equal(-7, results[1].NativeResult);
        Assert.NotNull(results[1].Error);
        Assert.Empty(results[1].SerialNumbers);
        Assert.Equal(EngineSpectrometerType.Gaolitong, results[2].Type);
        Assert.Equal("DEVICE-002", Assert.Single(results[2].SerialNumbers));
        Assert.Contains("Gaolitong", SpectrumDeviceDiscovery.FormatResults(results));
        Assert.Contains("DEVICE-002", SpectrumDeviceDiscovery.FormatResults(results));
    }

    [Fact]
    public void Discovery_ReportsMalformedPayloadAndContinues()
    {
        var results = SpectrumDeviceDiscovery.Discover(0, (type, port, buffer, length) =>
        {
            buffer.Append(type == 0 ? "invalid JSON" : "[]");
            return 1;
        });

        Assert.NotNull(results[0].Error);
        Assert.Equal(3, results.Count);
        Assert.All(results.Skip(1), result => Assert.Null(result.Error));
    }

    [Theory]
    [InlineData("{\"number\":0,\"ID\":[]}")]
    [InlineData("{\"number\":0,\"ID\":null}")]
    [InlineData("[]")]
    [InlineData("")]
    public void EmptyResults_DoNotTreatTheDeviceCountAsASerialNumber(string raw)
    {
        Assert.Empty(SpectrumDeviceDiscovery.ParseSerialNumbers(raw));
    }

    [Theory]
    [InlineData("{\"number\":4,\"ID\":[\" SN-001 \",\"SN-001\",\"\",null]}")]
    [InlineData("[\" SN-001 \",\"SN-001\",\"\",null]")]
    [InlineData("\"SN-001\"")]
    public void SerialNumbers_AreTrimmedAndDeduplicated(string raw)
    {
        Assert.Equal("SN-001", Assert.Single(SpectrumDeviceDiscovery.ParseSerialNumbers(raw)));
    }
}
