using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;

namespace ColorVision.UI.Tests;

public sealed class OpenCvOutputOwnershipTests
{
    [Fact]
    public async Task GaussianBlurPublishesIndependentPackedPixelsFromPaddedInput()
    {
        const int width = 19;
        const int height = 13;
        const AlgorithmImageFormat format = AlgorithmImageFormat.Bgr48;
        int rowBytes = width * format.BytesPerPixel();
        int stride = rowBytes + 16;
        byte[] padded = new byte[stride * height];
        new Random(17).NextBytes(padded);
        byte[] original = padded.ToArray();
        using AlgorithmImageBuffer input = new(width, height, stride, format, padded, 144, 192);
        using Mat expected = AlgorithmImageInterop.ToMat(input);
        Cv2.GaussianBlur(expected, expected, new Size(5, 5), 1.5);
        using AlgorithmImageBuffer expectedPixels = AlgorithmImageInterop.FromMat(expected);

        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRunner runner = new(StandardAlgorithmCatalog.Create(), [new OpenCvAlgorithmProvider()], scheduler);
        using AlgorithmResult result = await runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GaussianBlur, new GaussianBlurParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Assert.IsType<AlgorithmImageArtifact>(Assert.Single(result.Artifacts)).Image;
        Assert.Equal((width, height, rowBytes, format, 144d, 192d),
            (output.Width, output.Height, output.Stride, output.Format, output.DpiX, output.DpiY));
        Assert.Equal(original, input.Data.ToArray());
        Assert.NotEqual(input.StorageId, output.StorageId);
        input.Dispose();
        Assert.True(expectedPixels.Data.Span.SequenceEqual(output.Data.Span));
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr24, 513)]
    [InlineData(AlgorithmImageFormat.Bgra64, 1025)]
    [InlineData(AlgorithmImageFormat.Bgra128Float, 513)]
    public async Task WhiteBalanceMatchesSeparateChannelsForPaddedInput(AlgorithmImageFormat format, int width)
    {
        int height = width - 1;
        int rowBytes = width * format.BytesPerPixel();
        int stride = rowBytes + 16;
        byte[] padded = new byte[stride * height];
        new Random(89).NextBytes(padded);
        byte[] original = padded.ToArray();
        using AlgorithmImageBuffer input = new(width, height, stride, format, padded, 144, 192);
        WhiteBalanceParameters parameters = new() { BlueScale = 0.5, GreenScale = 1.6, RedScale = 3.25 };
        using Mat expected = new();
        Mat[] planes;
        using (AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input)) planes = Cv2.Split(source.Mat);
        try
        {
            planes[0].ConvertTo(planes[0], planes[0].Type(), parameters.BlueScale);
            planes[1].ConvertTo(planes[1], planes[1].Type(), parameters.GreenScale);
            planes[2].ConvertTo(planes[2], planes[2].Type(), parameters.RedScale);
            Cv2.Merge(planes, expected);
        }
        finally
        {
            foreach (Mat plane in planes) plane.Dispose();
        }
        using AlgorithmImageBuffer expectedPixels = AlgorithmImageInterop.FromMat(expected);

        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRunner runner = new(StandardAlgorithmCatalog.Create(), [new OpenCvAlgorithmProvider()], scheduler);
        using AlgorithmResult result = await runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.WhiteBalance, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Assert.IsType<AlgorithmImageArtifact>(Assert.Single(result.Artifacts)).Image;
        Assert.Equal((width, height, rowBytes, format, 144d, 192d),
            (output.Width, output.Height, output.Stride, output.Format, output.DpiX, output.DpiY));
        Assert.True(original.AsSpan().SequenceEqual(input.Data.Span));
        Assert.NotEqual(input.StorageId, output.StorageId);
        input.Dispose();
        Assert.True(expectedPixels.Data.Span.SequenceEqual(output.Data.Span));
    }
}
