using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;

namespace ColorVision.UI.Tests;

public sealed class ImageRegistrationOutputTests
{
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
