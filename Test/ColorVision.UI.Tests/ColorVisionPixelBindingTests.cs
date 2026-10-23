using ColorVision.ImageEditor.Tif;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ColorVisionPixelBindingTests
{
    [Fact]
    public void PixelBindingUsesCanonicalRgbAndIgnoresRowPadding()
    {
        WpfTestHost.Invoke(() =>
        {
            byte[] rgb = [10, 20, 30, 40, 50, 60];
            byte[] bgrWithPadding = [30, 20, 10, 60, 50, 40, 99, 98];
            BitmapSource rgbImage = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Rgb24, null, rgb, 6);
            BitmapSource bgrImage = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgr24, null, bgrWithPadding, 8);
            string expected = Convert.ToHexString(SHA256.HashData(rgb));
            Assert.Equal(expected, ColorVisionPixelBinding.ComputeSha256(rgbImage));
            Assert.Equal(expected, ColorVisionPixelBinding.ComputeSha256(bgrImage));
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => ColorVisionPixelBinding.ComputeSha256(rgbImage, cancellation.Token));
        });
    }

    [Fact]
    public void FloatBindingPreservesNegativeAndFractionalSamples()
    {
        WpfTestHost.Invoke(() =>
        {
            float[] samples = [-2.5f, 0.125f];
            byte[] bytes = new byte[8];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            BitmapSource image = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Gray32Float, null, bytes, bytes.Length);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), ColorVisionPixelBinding.ComputeSha256(image));
        });
    }

    [Fact]
    public void MetadataRetainsLegacyAndUnknownVersionsAsRecords()
    {
        Assert.True(ColorVisionTiffParameters.TryParse("{\"schema\":\"ColorVision.CVImage/1\",\"gain\":2}", out var legacy));
        Assert.Equal(2, legacy!.Gain);
        ColorVisionTiffParameters original = new()
        {
            Schema = "ColorVision.CVImage/99",
            PixelRole = "calibration-input",
            Calibration = JToken.FromObject(new { coefficients = new[] { 1, 2, 3 } }),
        };
        Assert.True(ColorVisionTiffParameters.TryParse(original.ToJson(), out var record));
        Assert.Equal(original.Schema, record!.Schema);
        Assert.Equal(3, Assert.IsType<JArray>(record.Calibration!["coefficients"]).Count);
        Assert.False(ColorVisionTiffParameters.TryParse("{broken", out _));
        Assert.False(ColorVisionTiffParameters.TryParse("{}", out _));
        Assert.False(ColorVisionTiffParameters.TryParse("[]", out _));
        Assert.False(ColorVisionTiffParameters.TryParse("null", out _));
        Assert.False(ColorVisionTiffParameters.TryParse("{\"schema\":2}", out _));
        Assert.True(ColorVisionTiffParameters.TryParse("{\"SCHEMA\":\"ColorVision.CVImage/1\",\"EXPOSURE\":null}", out var mixedCase));
        Assert.Empty(mixedCase!.Exposure);
    }

    [Fact]
    public void MetadataUsesAsciiSafeJsonWithoutChangingCalibrationFieldNamesOrStrings()
    {
        const string template = "中文模板🌈";
        const string timestamp = "2026-10-23T10:20:30.1234567+08:00";
        ColorVisionTiffParameters original = new()
        {
            InputFileName = "采集.cvraw",
            Calibration = JToken.FromObject(new { Template = template, CapturedAt = timestamp }),
        };
        string json = original.ToJson();
        Assert.All(json, value => Assert.InRange((int)value, 0, 127));
        Assert.True(ColorVisionTiffParameters.TryParse(json, out var record));
        Assert.Equal(original.InputFileName, record!.InputFileName);
        Assert.Equal(template, record.Calibration!["Template"]!.Value<string>());
        Assert.Equal(timestamp, record.Calibration["CapturedAt"]!.Value<string>());
        Assert.Null(record.Calibration["template"]);
    }
}
