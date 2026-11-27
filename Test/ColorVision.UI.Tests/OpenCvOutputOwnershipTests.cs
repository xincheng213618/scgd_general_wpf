using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System.Buffers.Binary;

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
    [InlineData(true, AlgorithmImageFormat.Gray8)]
    [InlineData(false, AlgorithmImageFormat.Bgr24)]
    public async Task FormatChangingOperationsPublishIndependentPackedPixels(bool canny, AlgorithmImageFormat expectedFormat)
    {
        const int width = 19;
        const int height = 13;
        const AlgorithmImageFormat sourceFormat = AlgorithmImageFormat.Bgra64;
        int stride = width * sourceFormat.BytesPerPixel() + 16;
        byte[] padded = new byte[stride * height];
        new Random(17).NextBytes(padded);
        byte[] original = padded.ToArray();
        using AlgorithmImageBuffer input = new(width, height, stride, sourceFormat, padded, 144, 192);
        using Mat source = AlgorithmImageInterop.ToMat(input);
        using Mat expected = new();
        CannyParameters cannyParameters = new();
        if (canny)
        {
            using Mat gray = new();
            using Mat gray8 = new();
            Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
            gray.ConvertTo(gray8, MatType.CV_8U, byte.MaxValue / (double)ushort.MaxValue);
            Cv2.Canny(gray8, expected, cannyParameters.LowThreshold, cannyParameters.HighThreshold, cannyParameters.ApertureSize, cannyParameters.L2Gradient);
        }
        else OpenCvAlgorithmProvider.ApplyHistogramEqualization(source, expected);
        using AlgorithmImageBuffer expectedPixels = AlgorithmImageInterop.FromMat(expected);

        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRunner runner = new(StandardAlgorithmCatalog.Create(), [new OpenCvAlgorithmProvider()], scheduler);
        using AlgorithmResult result = await runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = canny ? AlgorithmInvocation.Create(StandardAlgorithmIds.Canny, cannyParameters)
                : AlgorithmInvocation.Create(StandardAlgorithmIds.HistogramEqualization, new NoAlgorithmParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Assert.IsType<AlgorithmImageArtifact>(Assert.Single(result.Artifacts)).Image;
        Assert.Equal((width, height, width * expectedFormat.BytesPerPixel(), expectedFormat, 144d, 192d),
            (output.Width, output.Height, output.Stride, output.Format, output.DpiX, output.DpiY));
        Assert.True(original.AsSpan().SequenceEqual(input.Data.Span));
        Assert.NotEqual(input.StorageId, output.StorageId);
        input.Dispose();
        Assert.True(expectedPixels.Data.Span.SequenceEqual(output.Data.Span));
    }

    [Fact]
    public void CannyKeepsTheBoundaryOfAGray8SubmatrixIsolated()
    {
        const int width = 37, height = 33, stride = width + 5;
        byte[] pixels = new byte[stride * height];
        new Random(31).NextBytes(pixels);
        byte[] original = pixels.ToArray();
        using AlgorithmImageBuffer input = new(width, height, stride, AlgorithmImageFormat.Gray8, pixels);
        using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input);
        using Mat region = new(source.Mat, new OpenCvSharp.Rect(2, 3, width - 4, height - 6));
        using Mat detachedRegion = region.Clone();
        using Mat expected = new();
        using Mat actual = new();
        CannyParameters parameters = new() { ApertureSize = 5, L2Gradient = true };
        Cv2.Canny(detachedRegion, expected, parameters.LowThreshold, parameters.HighThreshold, parameters.ApertureSize, parameters.L2Gradient);
        OpenCvAlgorithmProvider.ApplyCanny(region, actual, parameters);

        using AlgorithmImageBuffer expectedPixels = AlgorithmImageInterop.FromMat(expected);
        using AlgorithmImageBuffer actualPixels = AlgorithmImageInterop.FromMat(actual);
        Assert.True(original.AsSpan().SequenceEqual(input.Data.Span));
        Assert.True(expectedPixels.Data.Span.SequenceEqual(actualPixels.Data.Span));
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Bgra64)]
    public async Task HistogramEqualizationMatchesOriginalDepthNormalization(AlgorithmImageFormat format)
    {
        const int width = 257;
        const int height = 131;
        int pixelBytes = format.BytesPerPixel();
        int channels = pixelBytes / sizeof(ushort);
        int stride = width * pixelBytes + 6;
        byte[] padded = new byte[stride * height];
        Array.Fill(padded, byte.MaxValue);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        for (int channel = 0; channel < channels; channel++)
        {
            ushort value = (ushort)(26_001 + (x * 13 + y * 43 + channel * 19) % 509);
            BinaryPrimitives.WriteUInt16LittleEndian(padded.AsSpan(y * stride + x * pixelBytes + channel * sizeof(ushort)), value);
        }
        // Include half-step rounding and let only alpha set the range endpoints for BGRA.
        BinaryPrimitives.WriteUInt16LittleEndian(padded.AsSpan(pixelBytes - sizeof(ushort)), 26_000);
        BinaryPrimitives.WriteUInt16LittleEndian(padded.AsSpan(stride + pixelBytes - sizeof(ushort)), 26_510);
        using AlgorithmImageBuffer input = new(width, height, stride, format, padded, 144, 192);
        using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input);
        using Mat normalized = new();
        Cv2.Normalize(source.Mat, normalized, 0, byte.MaxValue, NormTypes.MinMax);
        using Mat source8 = new();
        normalized.ConvertTo(source8, MatType.CV_8U);
        using Mat expected = new();
        OpenCvAlgorithmProvider.ApplyHistogramEqualization(source8, expected);
        using AlgorithmImageBuffer expectedPixels = AlgorithmImageInterop.FromMat(expected);

        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRunner runner = new(StandardAlgorithmCatalog.Create(), [new OpenCvAlgorithmProvider()], scheduler);
        using AlgorithmResult result = await runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.HistogramEqualization, new NoAlgorithmParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Assert.IsType<AlgorithmImageArtifact>(Assert.Single(result.Artifacts)).Image;
        Assert.Equal((width, height, expectedPixels.Stride, expectedPixels.Format, 144d, 192d),
            (output.Width, output.Height, output.Stride, output.Format, output.DpiX, output.DpiY));
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
