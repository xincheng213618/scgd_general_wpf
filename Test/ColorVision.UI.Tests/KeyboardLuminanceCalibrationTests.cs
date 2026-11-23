using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using cvColorVision;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class KeyboardLuminanceCalibrationTests
{
    [Theory]
    [InlineData(1.0f)]
    [InlineData(200.0f)]
    public void NativeLuminancePreservesGrayQuantizationExposureAndKeyOrder(float exposure)
    {
        string path = Path.Combine(Path.GetTempPath(), $"键盘亮度-{Guid.NewGuid():N}.dat");
        File.WriteAllText(path, """{"bpp":16,"a":0.125}""");
        using var cache = new LocalCalibrationCacheManager("keyboard-test");
        DeviceCameraCalibrationFile file = LuminanceFile(path);
        try
        {
            JArray measurements = new(
                new JObject { ["keyValid"] = false, ["keyMean"] = 0.9 },
                new JObject { ["keyValid"] = true, ["keyMean"] = 0.123456789 },
                new JValue("ignored"),
                new JObject { ["keyValid"] = true, ["keyMean"] = -0.1 },
                new JObject { ["keyValid"] = true, ["keyMean"] = 1.2 },
                new JObject { ["keyValid"] = true, ["keyMean"] = 0.5 });

            Assert.True(EditPoiParam1.TryCalibrateKeyboardMeans(cache, file, measurements, [exposure, 300, 400], out double[] values));
            Assert.True(double.IsNaN(values[0]));
            Assert.True(double.IsNaN(values[2]));
            foreach (int index in new[] { 1, 3, 4, 5 })
            {
                double mean = measurements[index].Value<double>("keyMean");
                ushort gray = (ushort)Math.Clamp(Math.Round(mean * ushort.MaxValue), 0, ushort.MaxValue);
                Assert.Equal((double)(float)(0.125 / exposure * gray), values[index]);
            }
            File.WriteAllText(path, """{"bpp":16,"a":0.25}""");
            Assert.True(EditPoiParam1.TryCalibrateKeyboardMeans(cache, file, measurements, [exposure, 300, 400], out values));
            Assert.Equal((double)(float)(0.25 / exposure * ushort.MaxValue), values[4]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NativeFailureLeavesNoCalibratedValuesForGrayFallback()
    {
        JArray measurements = new(new JObject { ["keyValid"] = true, ["keyMean"] = 0.5 });
        using var cache = new LocalCalibrationCacheManager("keyboard-test");
        Assert.False(EditPoiParam1.TryCalibrateKeyboardMeans(cache, LuminanceFile(Path.GetRandomFileName()), measurements,
            [100, 100, 100], out double[] values));
        Assert.True(double.IsNaN(Assert.Single(values)));
    }

    [Fact]
    public void NoValidKeysSkipCalibrationAndRetainMissingValues()
    {
        JArray measurements = new(new JObject { ["keyValid"] = false, ["keyMean"] = 0.5 });
        using var cache = new LocalCalibrationCacheManager("keyboard-test");
        Assert.True(EditPoiParam1.TryCalibrateKeyboardMeans(cache, LuminanceFile(Path.GetRandomFileName()), measurements,
            [100, 100, 100], out double[] values));
        Assert.True(double.IsNaN(Assert.Single(values)));
    }

    private static DeviceCameraCalibrationFile LuminanceFile(string path)
        => new("Luminance", CalibrationType.Luminance, "test", Path.GetFileName(path), path);
}
