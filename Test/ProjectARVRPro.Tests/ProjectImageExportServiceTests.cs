using ColorVision.ImageEditor;
using ProjectARVRPro.ImageExport;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ProjectImageExportServiceTests
{
    [Fact]
    public void FileNames_KeepRenderedAndSourceArtifactsDistinctAndSanitized()
    {
        string rendered = ProjectImageExportService.BuildResultFileStem(
            @"C:\capture\A:B.cvraw",
            "White/51");
        string source = ProjectImageExportService.BuildSourceFileStem(
            @"C:\capture\A:B.cvraw",
            "White/51");

        Assert.Equal("AB_White51result", rendered);
        Assert.Equal("AB_White51source", source);
        Assert.NotEqual(rendered, source);
    }

    [Fact]
    public void Extensions_AreDerivedFromTheStronglyTypedExportLane()
    {
        Assert.Equal(".png", ProjectImageExportService.GetResultExtension(ResultImageFormat.PNG));
        Assert.Equal(".jpg", ProjectImageExportService.GetResultExtension(ResultImageFormat.JPEG));
        Assert.Equal(".tif", ProjectImageExportService.GetSourceExtension(SourceImageFormat.TIFF));
        Assert.Equal(".png", ProjectImageExportService.GetSourceExtension(SourceImageFormat.PNG));
        Assert.Equal(".bmp", ProjectImageExportService.GetSourceExtension(SourceImageFormat.BMP));
    }

    [Theory]
    [InlineData(ImageExportSize.完整尺寸, 1)]
    [InlineData(ImageExportSize.二分之一尺寸, 2)]
    [InlineData(ImageExportSize.四分之一尺寸, 4)]
    public void RenderedImageSize_MapsToExpectedDivisor(ImageExportSize size, int expectedDivisor)
    {
        Assert.Equal(expectedDivisor, ProjectImageExportService.GetScaleDivisor(size));
    }

    [Fact]
    public void ImageEditorOptions_KeepLaneSpecificDefaultsAndCompressionMapping()
    {
        ImageViewSnapshotSaveOptions rendered = ProjectImageExportService.CreateRenderedOptions(
            ResultImageFormat.JPEG,
            ImageExportSize.四分之一尺寸);
        Assert.Equal(ImageViewSnapshotFormat.Jpeg, rendered.Format);
        Assert.Equal(4, rendered.ScaleDivisor);
        Assert.Equal(100, rendered.JpegQuality);

        ImageViewSourceSaveOptions lzw = ProjectImageExportService.CreateSourceOptions(
            SourceImageFormat.TIFF,
            SourceTiffCompression.LZW);
        ImageViewSourceSaveOptions zip = ProjectImageExportService.CreateSourceOptions(
            SourceImageFormat.TIFF,
            SourceTiffCompression.ZIP);
        ImageViewSourceSaveOptions bmp = ProjectImageExportService.CreateSourceOptions(
            SourceImageFormat.BMP,
            SourceTiffCompression.LZW);

        Assert.Equal(ImageViewSourceFormat.Tiff, lzw.Format);
        Assert.Equal(ImageViewTiffCompression.Lzw, lzw.TiffCompression);
        Assert.Equal(ImageViewTiffCompression.Zip, zip.TiffCompression);
        Assert.Equal(ImageViewSourceFormat.Bmp, bmp.Format);
    }
}
