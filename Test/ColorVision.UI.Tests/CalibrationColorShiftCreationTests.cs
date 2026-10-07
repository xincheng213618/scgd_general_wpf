using ColorVision.Core;
using ColorVision.Engine.Services.PhyCameras.Calibration.Creation;
using ColorVision.FileIO;
using cvColorVision;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class CalibrationColorShiftCreationTests
{
    [Fact]
    public void IntegerOffsetsUseBgrOrderAndValidationDoesNotInfluenceFit()
    {
        var frames = new[] { Points("a", 2.7f), Points("b", 2.7f), Points("validation", 3.4f) };
        var fit = CalibrationColorShiftCreation.FitPoints(frames, 100, 80, 0.5);
        Assert.Equal(4, fit.Json["offset"]![0]!["X"]!.Value<int>());
        Assert.Equal(0, fit.Json["offset"]![1]!["X"]!.Value<int>());
        Assert.Equal(-3, fit.Json["offset"]![2]!["X"]!.Value<int>());
        Assert.InRange(fit.Validation.MaximumPixels, 0.399, 0.401);
        Assert.True(fit.Validation.RmsPixels < fit.BeforeValidation.RmsPixels);
        // A held-out failure must not be hidden by refitting with the validation image.
        frames[2] = Points("validation", 5);
        Assert.Throws<InvalidDataException>(() => CalibrationColorShiftCreation.FitPoints(frames, 100, 80, 0.5));
    }

    [Fact]
    public void MismatchesDegenerateCoordinatesAndInvalidLimitsCannotProduceCalibration()
    {
        var frames = new[] { Points("a", 3), Points("b", 3), Points("validation", 3) };
        frames[0].Red[0] = new Point2f(70, 60);
        Assert.Throws<InvalidDataException>(() => CalibrationColorShiftCreation.FitPoints(frames, 100, 80, 1));
        frames[0] = Points("a", 3);
        frames[2].Green[0] = new Point2f(float.NaN, 10);
        Assert.Throws<InvalidDataException>(() => CalibrationColorShiftCreation.FitPoints(frames, 100, 80, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalibrationColorShiftCreation.FitPoints(frames, 100, 80, double.NaN));
        frames[2] = Points("a", 3);
        Assert.Throws<InvalidDataException>(() => CalibrationColorShiftCreation.FitPoints(frames, 100, 80, 1));
        var line = Enumerable.Range(0, 6).Select(i => new Point2f(i + 10, 10)).ToArray();
        frames[2] = new("validation", line, line, line);
        Assert.Throws<InvalidDataException>(() => CalibrationColorShiftCreation.FitPoints(frames, 100, 80, 1));
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(16, false)]
    public void RawChessboardsGenerateOffsetsThatActuallyAlignNativePixels(int bits, bool bgr)
    {
        string[] paths = Enumerable.Range(0, 3).Select(_ => Path.Combine(Path.GetTempPath(), $"cv-shift-{Guid.NewGuid():N}.cvraw")).ToArray();
        string jsonPath = Path.Combine(Path.GetTempPath(), $"cv-shift-{Guid.NewGuid():N}.json");
        const int width = 300, height = 240;
        IntPtr context = IntPtr.Zero;
        try
        {
            byte[] pixels = Array.Empty<byte>();
            for (int i = 0; i < paths.Length; i++) pixels = WriteChessboard(paths[i], width, height, bits, bgr, i * 5, i * 3);
            var fit = CalibrationColorShiftCreation.Fit(paths, 9, 6, 0.1, bgr);
            Assert.Equal(-3, fit.Json["offset"]![2]!["X"]!.Value<int>());
            Assert.Equal(2, fit.Json["offset"]![2]!["Y"]!.Value<int>());
            Assert.Equal(4, fit.Json["offset"]![0]!["X"]!.Value<int>());
            Assert.Equal(-1, fit.Json["offset"]![0]!["Y"]!.Value<int>());
            File.WriteAllText(jsonPath, fit.Json.ToString());
            Assert.Equal(1, OpenCVCalibration.M_CalibrationCreate(out context));
            Assert.Equal(1, OpenCVCalibration.M_CalibrationLoadFileW(context, (int)CalibrationType.ColorShift, jsonPath));
            var options = CalibrationExecutionOptionsV1.Create(new float[] { 10, 10, 10 });
            options.InterleavedBgr = bgr ? 1 : 0;
            GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                Assert.Equal(1, OpenCVCalibration.M_CalibrationExecute(context, width, height, (uint)bits, 3,
                    pin.AddrOfPinnedObject(), (ulong)pixels.Length, IntPtr.Zero, 0, in options));
            }
            finally { pin.Free(); }
            // ColorShift emits BGR for both supported input layouts. Check the board, not just coefficients.
            for (int y = 20; y < height - 20; y++)
                for (int x = 20; x < width - 20; x++)
                {
                    int index = (y * width + x) * 3 * bits / 8;
                    for (int channel = 1; channel < 3; channel++)
                        for (int part = 0; part < bits / 8; part++) Assert.Equal(pixels[index + part], pixels[index + channel * bits / 8 + part]);
                }
            Assert.Throws<InvalidDataException>(() => CalibrationColorShiftCreation.Fit(new[] { paths[0], paths[1], paths[0] }, 9, 6, 1, bgr));
            CVFileMetadata.SetProperty(paths[0], "colorvision.calibration.color", 1, new byte[] { 1 });
            Assert.Throws<InvalidDataException>(() => CalibrationColorShiftCreation.Fit(paths, 9, 6, 1, bgr));
        }
        finally
        {
            if (context != IntPtr.Zero) OpenCVCalibration.M_CalibrationDestroy(context);
            foreach (string path in paths.Append(jsonPath)) File.Delete(path);
        }
    }

    private static CalibrationChannelPoints Points(string id, float redX)
    {
        Point2f[] green = Enumerable.Range(0, 6).Select(i => new Point2f(20 + i % 3 * 10, 20 + i / 3 * 10)).ToArray();
        return new(id, green.Select(p => new Point2f(p.X + redX, p.Y - 2)).ToArray(), green,
            green.Select(p => new Point2f(p.X - 4, p.Y + 1)).ToArray());
    }

    private static byte[] WriteChessboard(string path, int width, int height, int bits, bool bgr, int boardX, int boardY)
    {
        using MemoryStream stream = new();
        using BinaryWriter data = new(stream);
        for (int index = 0; index < width * height * 3; index++)
        {
            int channel = bgr ? 2 - index % 3 : index / (width * height);
            int pixel = bgr ? index / 3 : index % (width * height);
            int x = pixel % width - (channel == 0 ? 3 : channel == 2 ? -4 : 0) - 40 - boardX;
            int y = pixel / width - (channel == 0 ? -2 : channel == 2 ? 1 : 0) - 40 - boardY;
            int value = x >= 0 && x < 200 && y >= 0 && y < 140 && (x / 20 + y / 20) % 2 == 0 ? 30 : 190;
            if (bits == 8) data.Write((byte)value); else data.Write((ushort)(value * 128));
        }
        byte[] pixels = stream.ToArray();
        using BinaryWriter writer = new(File.Create(path));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("CVCIE"));
        writer.Write(2u); writer.Write(0); writer.Write(1f); writer.Write(3);
        for (int i = 0; i < 3; i++) writer.Write(10f);
        writer.Write(width); writer.Write(height); writer.Write(bits); writer.Write((long)pixels.Length); writer.Write(pixels);
        return pixels;
    }
}
