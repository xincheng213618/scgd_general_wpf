extern alias HikMvsSdk;
using MyCamera = HikMvsSdk::MvCamCtrl.NET.MyCamera;
using PixelType = HikMvsSdk::MvCamCtrl.NET.MyCamera.MvGvspPixelType;
using ColorVision.Engine.Services.Devices.Camera.Diagnostics;
using cvColorVision;
using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
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

    [Theory]
    [InlineData(PixelType.PixelType_Gvsp_Mono8)]
    [InlineData(PixelType.PixelType_Gvsp_RGB8_Packed)]
    public void MissingMono16AndAmbiguousBayerPhasesAreRejected(PixelType current)
    {
        MyCamera.MVCC_ENUMVALUE formats = new()
        {
            nCurValue = (uint)current, nSupportedNum = 2,
            nSupportValue = [(uint)PixelType.PixelType_Gvsp_BayerRG16, (uint)PixelType.PixelType_Gvsp_BayerBG16]
        };
        Assert.Throws<InvalidOperationException>(() => HikCaptureBenchmark.SelectFormat(formats));
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
        byte[] payload = [0, 1, 128, 255, 0, 0, 12, 34];
        IntPtr buffer = Marshal.AllocHGlobal(payload.Length);
        try
        {
            Marshal.Copy(payload, 0, buffer, payload.Length);
            HikRawFrameExport.Save(path, buffer, payload.Length, rawFile, new { RawFile = rawFile }, CancellationToken.None);
            using var zip = ZipFile.OpenRead(path);
            using var source = zip.GetEntry(rawFile)!.Open();
            using var output = new MemoryStream();
            source.CopyTo(output);
            Assert.Equal(payload, output.ToArray());
            using var metadata = new StreamReader(zip.GetEntry("metadata.json")!.Open());
            Assert.Contains(rawFile, metadata.ReadToEnd());
            Assert.Equal(2, zip.Entries.Count);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            File.Delete(path);
        }
    }
}
