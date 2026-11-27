using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class ImageRegistrationOutputTests
{
    [Theory]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Bgr48)]
    public async Task LargePaddedUInt16RegistrationMeasuresOnlyMaskedPhotometricDifferences(AlgorithmImageFormat format)
    {
        const int width = 1025, height = 1025;
        const int shiftX = 13, shiftY = 7;
        int channels = format.Channels();
        int rowSamples = width * channels + 3;
        ushort[] referencePixels = new ushort[rowSamples * height];
        ushort[] movingPixels = new ushort[rowSamples * height];
        Random random = new(173);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            ushort pixel = random.Next(2) == 0 ? (ushort)0 : ushort.MaxValue;
            referencePixels.AsSpan(y * rowSamples + x * channels, channels).Fill(pixel);
            if (x + shiftX < width && y + shiftY < height)
                movingPixels.AsSpan((y + shiftY) * rowSamples + (x + shiftX) * channels, channels)
                    .Fill((x + 3 * y) % 7 == 0 ? (ushort)(ushort.MaxValue - pixel) : pixel);
        }

        using AlgorithmImageBuffer reference = new(width, height, rowSamples * sizeof(ushort), format,
            MemoryMarshal.AsBytes(referencePixels.AsSpan()).ToArray());
        using AlgorithmImageBuffer moving = new(width, height, rowSamples * sizeof(ushort), format,
            MemoryMarshal.AsBytes(movingPixels.AsSpan()).ToArray());
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageRegistration,
                new ImageRegistrationParameters { Interpolation = GeometricTransformInterpolation.Nearest, UseHannWindow = false }),
            Inputs =
            [
                new AlgorithmInput { Name = "reference", Image = reference, ColorSpace = "encoded-device-values" },
                new AlgorithmInput { Name = "moving", Image = moving, ColorSpace = "encoded-device-values" },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });
        Assert.True(result.Status == AlgorithmResultStatus.Succeeded, string.Join(Environment.NewLine, result.Failures.Select(failure => failure.Message)));
        AlgorithmImageBuffer registered = result.GetArtifact<AlgorithmImageArtifact>("registered-image")!.Image;
        AlgorithmImageBuffer mask = result.GetArtifact<AlgorithmImageArtifact>("valid-region-mask")!.Image;
        ReadOnlySpan<ushort> registeredPixels = MemoryMarshal.Cast<byte, ushort>(registered.Data.Span);
        ReadOnlySpan<byte> validMask = mask.Data.Span;
        int valid = 0, different = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (validMask[y * width + x] == 0) continue;
            valid++;
            if (referencePixels[y * rowSamples + x * channels] != registeredPixels[(y * width + x) * channels]) different++;
        }

        Assert.InRange(valid, 1, width * height - 1);
        Assert.InRange(different, 1, valid - 1);
        // Normalized endpoints are exactly 0 and 1, so every nonzero squared residual is one.
        double expectedRmse = Math.Sqrt(different / (double)valid);
        double actualRmse = result.GetArtifact<AlgorithmMeasurementArtifact>("image-registration-summary")!.Measurements
            .Single(value => value.Name == "registration.photometric_rmse").Value;
        Assert.Equal(expectedRmse, actualRmse, 12);
    }

    [Fact]
    public async Task RegisteredImageOwnsPackedPixelsFromPaddedInputs()
    {
        const int width = 96, height = 64, stride = width + 5;
        byte[] pixels = new byte[stride * height];
        new Random(431).NextBytes(pixels);
        byte[] expected = new byte[width * height];
        for (int y = 0; y < height; y++)
            pixels.AsSpan(y * stride, width).CopyTo(expected.AsSpan(y * width));
        using AlgorithmImageBuffer reference = new(width, height, stride, AlgorithmImageFormat.Gray8, pixels, 146, 123);
        using AlgorithmImageBuffer moving = reference.Clone();
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageRegistration,
                new ImageRegistrationParameters { Interpolation = GeometricTransformInterpolation.Nearest }),
            Inputs =
            [
                new AlgorithmInput { Name = "reference", Image = reference, ColorSpace = "encoded-device-values" },
                new AlgorithmInput { Name = "moving", Image = moving, ColorSpace = "encoded-device-values" },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });

        Assert.True(result.Status == AlgorithmResultStatus.Succeeded, string.Join(Environment.NewLine, result.Failures.Select(failure => failure.Message)));
        AlgorithmImageBuffer registered = result.GetArtifact<AlgorithmImageArtifact>("registered-image")!.Image;
        Assert.Equal((width, height, width, AlgorithmImageFormat.Gray8, 146d, 123d),
            (registered.Width, registered.Height, registered.Stride, registered.Format, registered.DpiX, registered.DpiY));
        Assert.NotEqual(moving.StorageId, registered.StorageId);
        reference.Dispose();
        moving.Dispose();
        Assert.Equal(expected, registered.Data.ToArray());
    }
}
