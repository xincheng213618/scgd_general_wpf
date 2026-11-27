using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class DisplayDefectMeasurementTests
{
    [Theory]
    [InlineData(AlgorithmImageFormat.Gray16, 1d)]
    [InlineData(AlgorithmImageFormat.Gray16, 2.2)]
    [InlineData(AlgorithmImageFormat.Bgr48, 2.2)]
    public async Task PaddedUInt16MeasurementsMatchPredecodedGrayFloat(AlgorithmImageFormat format, double exponent)
    {
        const int width = 533, height = 497;
        const int floatStride = width * sizeof(float);
        int pixelBytes = format.BytesPerPixel();
        int selectedChannel = format == AlgorithmImageFormat.Gray16 ? 0 : 1;
        int stride = width * pixelBytes + 5;
        byte[] raw = new byte[stride * height];
        byte[] decoded = new byte[floatStride * height];
        Array.Fill(raw, byte.MaxValue);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            ushort sample = (ushort)(24_000 + (x + 3 * y) % 1_000);
            if (x == 101 && y == 123) sample = 48_321;
            if (x == 173 && y == 159) sample = 5_413;
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(y * stride + x * pixelBytes + selectedChannel * sizeof(ushort)), sample);
            BinaryPrimitives.WriteSingleLittleEndian(decoded.AsSpan(y * floatStride + x * sizeof(float)),
                (float)Math.Pow(sample / (double)ushort.MaxValue, exponent));
        }

        using var source = new AlgorithmImageBuffer(width, height, stride, format, raw);
        using var expectedSource = new AlgorithmImageBuffer(width, height, floatStride, AlgorithmImageFormat.Gray32Float, decoded);
        using AlgorithmResult result = await Run(source, selectedChannel, exponent);
        using AlgorithmResult expected = await Run(expectedSource, 0, 1);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(AlgorithmResultStatus.Succeeded, expected.Status);
        var expectedRows = expected.GetArtifact<AlgorithmTableArtifact>("defect-candidates")!.Rows;
        Assert.NotEmpty(expectedRows);
        Assert.Equal(JsonSerializer.Serialize(expectedRows), JsonSerializer.Serialize(result.GetArtifact<AlgorithmTableArtifact>("defect-candidates")!.Rows));
        Assert.True(expected.GetArtifact<AlgorithmImageArtifact>("candidate-bounds")!.Image.Data.Span.SequenceEqual(
            result.GetArtifact<AlgorithmImageArtifact>("candidate-bounds")!.Image.Data.Span));

        static ValueTask<AlgorithmResult> Run(AlgorithmImageBuffer image, int channel, double decodeExponent)
            => ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Defects, new DisplayDefectParameters { Channel = channel, DecodeExponent = decodeExponent }),
                Inputs = [new AlgorithmInput { Image = image, Name = "punctate-defect" }],
                RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            });
    }

    [Fact]
    public async Task TwoResidualScalesPreserveDefectPolarityAndSourcePixels()
    {
        const int width = 321, height = 257, stride = width * 2 + 3;
        var pixels = new byte[stride * height];
        Array.Fill(pixels, byte.MaxValue);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int value = 24000;
            if (x == 82 && y == 86) value += 18000;
            if (x == 137 && y == 89) value -= 18000;
            if (y == 124 && x is >= 62 and <= 125) value += 17000;
            if (y == 134 && x is >= 145 and <= 205) value -= 17000;
            if (InsideMura(x - 211, y - 170)) value += 9500;
            if (InsideMura(x - 108, y - 183)) value -= 9500;
            BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * stride + x * 2), (ushort)value);
        }

        byte[] original = (byte[])pixels.Clone();
        using var image = new AlgorithmImageBuffer(width, height, stride, AlgorithmImageFormat.Gray16, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Defects, new DisplayDefectParameters()),
            Inputs = [new AlgorithmInput { Image = image, Name = "uniform-field" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.True(image.Data.Span.SequenceEqual(original));
        var candidates = result.GetArtifact<AlgorithmTableArtifact>("defect-candidates")!.Rows;
        AlgorithmImageBuffer map = result.GetArtifact<AlgorithmImageArtifact>("candidate-bounds")!.Image;
        Assert.Equal((width, height), (map.Width, map.Height));
        foreach (var (kind, x, y, mapValue) in new (string, int, int, byte)[]
        {
            ("bright_point_candidate", 82, 86, 255), ("dark_point_candidate", 137, 89, 255),
            ("bright_line_candidate", 92, 124, 255), ("dark_line_candidate", 180, 134, 255),
            ("bright_mura_candidate", 211, 170, 127), ("dark_mura_candidate", 108, 183, 127),
        })
        {
            Assert.Contains(candidates, row =>
            {
                int left = row["x_px"].GetInt32(), top = row["y_px"].GetInt32();
                return row["kind"].GetString() == kind && left <= x && x < left + row["width_px"].GetInt32()
                    && top <= y && y < top + row["height_px"].GetInt32();
            });
            Assert.Equal(mapValue, map.Data.Span[y * map.Stride + x]);
        }
        Assert.Equal((byte)0, map.Data.Span[0]);

        static bool InsideMura(int dx, int dy) => dx * dx + dy * dy <= 14 * 14;
    }
}
