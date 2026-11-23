extern alias HikMvsSdk;
using MyCamera = HikMvsSdk::MvCamCtrl.NET.MyCamera;
using PixelType = HikMvsSdk::MvCamCtrl.NET.MyCamera.MvGvspPixelType;
using ColorVision.Engine.Services.Devices.Camera.Diagnostics;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class HikCaptureBenchmarkTests
{
    [Theory]
    [InlineData(CameraModel.HK_USB)]
    [InlineData(CameraModel.HK_CARD)]
    public void DiagnosticLaunchSettingsPreserveCameraTransport(CameraModel model)
    {
        var settings = new HikCaptureTestSettings { CameraId = "CAMERA-SERIAL", CameraModel = model };
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(settings)));

        var parsed = HikCaptureTestSettings.Parse([HikCaptureTestSettings.LaunchArgument, payload]);

        Assert.True(HikCaptureTestSettings.SupportsModel(parsed.CameraModel));
        Assert.Equal(model, parsed.CameraModel);
        Assert.Equal(settings.CameraId, parsed.CameraId);
        Assert.False(HikCaptureTestSettings.SupportsModel((CameraModel)int.MaxValue));
    }

    [Fact]
    public void LegacyDiagnosticSettingsDefaultToUsb()
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"CameraId\":\"USB-SERIAL\"}"));
        Assert.Equal(CameraModel.HK_USB, HikCaptureTestSettings.Parse([HikCaptureTestSettings.LaunchArgument, payload]).CameraModel);
    }

    [Theory]
    [InlineData(PixelType.PixelType_Gvsp_Mono8, PixelType.PixelType_Gvsp_Mono16, 1u, "frame.mono16.raw")]
    [InlineData(PixelType.PixelType_Gvsp_BayerRG12, PixelType.PixelType_Gvsp_BayerRG16, 3u, "frame.bayer16.raw")]
    public void OutputAndRawExportFollowTheSensorFormat(PixelType current, PixelType expected, uint channels, string rawFile)
    {
        MyCamera.MVCC_ENUMVALUE formats = new()
        {
            nCurValue = (uint)current, nSupportedNum = 3,
            nSupportValue = [(uint)expected, (uint)PixelType.PixelType_Gvsp_BayerBG16, (uint)PixelType.PixelType_Gvsp_Mono16]
        };
        var selected = HikCaptureBenchmark.SelectFormat(formats);

        Assert.Equal(expected, selected.Source);
        Assert.Equal(channels, selected.Channels);
        Assert.Equal(rawFile, selected.RawFileName);
        Assert.Equal(channels == 1 ? "Mono16" : "RGB48", selected.OutputFormat);
        Assert.Equal(checked(4 * 2 * (int)channels * 2), HikCaptureBenchmark.OutputLength(4, 2, channels));
    }

    [Fact]
    public void UnknownSensorPhaseWithMultipleBayerCandidatesIsRejected()
    {
        MyCamera.MVCC_ENUMVALUE formats = new()
        {
            nCurValue = (uint)PixelType.PixelType_Gvsp_RGB8_Packed, nSupportedNum = 2,
            nSupportValue = [(uint)PixelType.PixelType_Gvsp_BayerRG16, (uint)PixelType.PixelType_Gvsp_BayerBG16]
        };
        Assert.Throws<InvalidOperationException>(() => HikCaptureBenchmark.SelectFormat(formats));
    }

    [Theory]
    [InlineData(PixelType.PixelType_Gvsp_Mono8, PixelType.PixelType_Gvsp_Mono16, 1u)]
    [InlineData(PixelType.PixelType_Gvsp_Mono12, PixelType.PixelType_Gvsp_Mono16, 1u)]
    [InlineData(PixelType.PixelType_Gvsp_BayerRG8, PixelType.PixelType_Gvsp_BayerRG16, 3u)]
    [InlineData(PixelType.PixelType_Gvsp_BayerGR8, PixelType.PixelType_Gvsp_BayerGR16, 3u)]
    [InlineData(PixelType.PixelType_Gvsp_BayerGB8, PixelType.PixelType_Gvsp_BayerGB16, 3u)]
    [InlineData(PixelType.PixelType_Gvsp_BayerBG8, PixelType.PixelType_Gvsp_BayerBG16, 3u)]
    public void KnownPhaseSelectsDepthForSdkVerificationWhenCurrentEnumerationOmitsIt(PixelType current, PixelType expected, uint channels)
    {
        MyCamera.MVCC_ENUMVALUE formats = new()
        {
            nCurValue = (uint)current, nSupportedNum = 1, nSupportValue = [(uint)current]
        };
        var selected = HikCaptureBenchmark.SelectFormat(formats);

        Assert.Equal(expected, selected.Source);
        Assert.Equal(channels, selected.Channels);
    }

    [Fact]
    public void OutputAllocationRejectsOversizedFrames()
    {
        Assert.Throws<OverflowException>(() => HikCaptureBenchmark.OutputLength(uint.MaxValue, uint.MaxValue, 3));
    }

    [Theory]
    [InlineData("frame.mono16.raw")]
    [InlineData("frame.bayer16.raw")]
    public void RawExportKeepsExactSourceBytesAndTheDeclaredEntry(string rawFile)
    {
        string path = Path.Combine(Path.GetTempPath(), $"HikRawTest-{Guid.NewGuid():N}.zip");
        bool color = rawFile == "frame.bayer16.raw";
        byte[] payload = color ? new byte[512 * 512 * sizeof(ushort)] : [0, 1, 128, 255, 0, 0, 12, 34];
        byte[] reference = color ? new byte[payload.Length * 3] : [];
        if (color)
        {
            var random = new Random(31);
            random.NextBytes(payload);
            random.NextBytes(reference);
        }
        IntPtr buffer = Marshal.AllocHGlobal(payload.Length);
        try
        {
            Marshal.Copy(payload, 0, buffer, payload.Length);
            HikRawFrameExport.Save(path, buffer, payload.Length, rawFile, new { RawFile = rawFile }, reference, CancellationToken.None);
            using var zip = ZipFile.OpenRead(path);
            using var source = zip.GetEntry(rawFile)!.Open();
            using var output = new MemoryStream();
            source.CopyTo(output);
            Assert.Equal(payload, output.ToArray());
            using var metadata = new StreamReader(zip.GetEntry("metadata.json")!.Open());
            var manifest = JObject.Parse(metadata.ReadToEnd());
            Assert.Equal(rawFile, manifest.Value<string>("RawFile"));
            Assert.Equal(color ? 2 : 1, manifest.Value<int>("SchemaVersion"));
            if (color)
            {
                var sdk = Assert.IsType<JObject>(manifest["SdkReference"]);
                Assert.Equal("RGB16_Packed", sdk.Value<string>("PixelFormat"));
                Assert.Equal("little-endian", sdk.Value<string>("ByteOrder"));
                Assert.Equal(reference.Length, sdk.Value<int>("ByteLength"));
                Assert.Equal(Convert.ToHexString(SHA256.HashData(reference)), sdk.Value<string>("Sha256"));
            }
            else Assert.Null(manifest["SdkReference"]);
            Assert.Equal(2, zip.Entries.Count);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            File.Delete(path);
        }
    }
}
