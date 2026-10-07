using ColorVision.Core;
using ColorVision.Engine.Templates.POI;
using cvColorVision;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;

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
        IntPtr context = IntPtr.Zero;
        try
        {
            Assert.Equal(OpenCVCalibration.CalibrationOk, OpenCVCalibration.M_CalibrationCreate(out context));
            Assert.Equal(OpenCVCalibration.CalibrationOk,
                OpenCVCalibration.M_CalibrationLoadFileW(context, (int)CalibrationType.Luminance, path));
            JArray measurements = new(
                new JObject { ["keyValid"] = false, ["keyMean"] = 0.9 },
                new JObject { ["keyValid"] = true, ["keyMean"] = 0.123456789 },
                new JValue("ignored"),
                new JObject { ["keyValid"] = true, ["keyMean"] = -0.1 },
                new JObject { ["keyValid"] = true, ["keyMean"] = 1.2 },
                new JObject { ["keyValid"] = true, ["keyMean"] = 0.5 });

            Assert.True(Calibrate(context, measurements, [exposure, 300, 400], out double[] values));
            Assert.True(double.IsNaN(values[0]));
            Assert.True(double.IsNaN(values[2]));
            foreach (int index in new[] { 1, 3, 4, 5 })
            {
                double mean = measurements[index].Value<double>("keyMean");
                ushort gray = (ushort)Math.Clamp(Math.Round(mean * ushort.MaxValue), 0, ushort.MaxValue);
                Assert.Equal((double)(float)(0.125 / exposure * gray), values[index]);
            }
        }
        finally
        {
            if (context != IntPtr.Zero) _ = OpenCVCalibration.M_CalibrationDestroy(context);
            File.Delete(path);
        }
    }

    [Fact]
    public void NativeFailureLeavesNoCalibratedValuesForGrayFallback()
    {
        JArray measurements = new(new JObject { ["keyValid"] = true, ["keyMean"] = 0.5 });
        Assert.False(Calibrate(IntPtr.Zero, measurements, [100, 100, 100], out double[] values));
        Assert.True(double.IsNaN(Assert.Single(values)));
    }

    [Fact]
    public void NoValidKeysSkipCalibrationAndRetainMissingValues()
    {
        JArray measurements = new(new JObject { ["keyValid"] = false, ["keyMean"] = 0.5 });
        Assert.True(Calibrate(IntPtr.Zero, measurements, [100, 100, 100], out double[] values));
        Assert.True(double.IsNaN(Assert.Single(values)));
    }

    private static bool Calibrate(IntPtr context, JArray measurements, float[] exposure, out double[] values)
    {
        MethodInfo method = typeof(EditPoiParam1).GetMethod("TryCalibrateKeyboardMeans", BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] arguments = [context, measurements, exposure, null];
        bool success = (bool)method.Invoke(null, arguments)!;
        values = (double[])arguments[3]!;
        return success;
    }
}
