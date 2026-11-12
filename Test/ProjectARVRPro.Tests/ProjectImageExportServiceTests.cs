using ColorVision.ImageEditor;
using ProjectARVRPro.ImageExport;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ProjectImageExportServiceTests
{
    [Fact]
    public void DiagnosticContextIncludesStableCorrelationFieldsOnOneLine()
    {
        string context = ProjectImageExportService.BuildDiagnosticContext(
            "SN\r\n42",
            resultId: 17,
            batchId: 23,
            model: "White\n255");

        Assert.Equal(
            "Task=ProjectARVRPro.ImageExport SN=SN  42 ResultId=17 BatchId=23 Model=White 255",
            context);
    }

    [Theory]
    [InlineData(null, null, "AB_White51result", "AB_White51source")]
    [InlineData(ProjectImageExportService.DefaultResultFileNameTemplate, null, "White51", "AB_White51source")]
    [InlineData(null, ProjectImageExportService.DefaultSourceFileNameTemplate, "AB_White51result", "White51_source")]
    [InlineData("{FlowName}", "{FlowName}_source", "White51", "White51_source")]
    [InlineData("AOI_{SN}_{FlowName}", "raw_{FlowName}", "AOI_SN42_White51", "raw_White51")]
    public void FileNames_UseEachTemplateIndependentlyAndSanitizeValues(
        string? resultTemplate, string? sourceTemplate, string expectedRendered, string expectedSource)
    {
        var names = ProjectImageExportService.BuildFileStems(
            @"C:\capture\A:B.cvraw", "White/51", "SN:42", resultTemplate, sourceTemplate);

        Assert.Equal((expectedRendered, expectedSource), names);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{FlowName")]
    [InlineData("{Unknown}")]
    [InlineData(@"..\{FlowName}")]
    [InlineData("{FlowName}.png")]
    public void InvalidStoredTemplatesFallBackToTheChannelDefaults(string template)
    {
        Assert.False(ProjectImageExportService.IsValidFileNameTemplate(template));
        var names = ProjectImageExportService.BuildFileStems("Capture.cvraw", "White51", null, template, template);
        Assert.Equal(("White51", "White51_source"), names);
    }

    [Theory]
    [InlineData(".png", "WHITE51_source.png")]
    [InlineData(".tif", "WHITE51.tif")]
    public void RawExportKeepsTheMarkedPngWhenTemplatesResolveToTheSameName(string sourceExtension, string expected)
    {
        const string directory = @"C:\exports";
        string markedPath = ProjectImageExportService.BuildFilePath(directory, "White51", ".png");
        string rawPath = ProjectImageExportService.BuildSourceFilePath(directory, "WHITE51", sourceExtension, markedPath);
        Assert.Equal(System.IO.Path.Combine(directory, expected), rawPath);
    }

    [Theory]
    [InlineData(true, "INPUT_result.png", "input_result_source.png")]
    [InlineData(false, "input_source.png", "INPUT_source_source.png")]
    public void PublishingImageExportsPreservesTheInputFile(bool renderedTargetsInput, string expectedRendered, string expectedSource)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro_ImageExport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "input.png");
        string inputAlias = Path.Combine(directory, ".", "INPUT.png");
        try
        {
            File.WriteAllBytes(input, [1, 2, 3]);
            string rendered = renderedTargetsInput ? inputAlias : Path.Combine(directory, "input_source.png");
            string source = renderedTargetsInput ? Path.Combine(directory, "input_result.png") : inputAlias;
            using var attempt = new ProjectImageExportAttempt(rendered, source, input);
            File.WriteAllBytes(attempt.RenderedStagingFileName!, [4, 5]);
            File.WriteAllBytes(attempt.SourceStagingFileName!, [6, 7]);

            ProjectImageExportAttemptResult result = attempt.CommitSuccessfulChannels();

            Assert.Equal<byte>([1, 2, 3], File.ReadAllBytes(input));
            Assert.Equal(Path.Combine(directory, expectedRendered), result.RenderedFileName);
            Assert.Equal(Path.Combine(directory, expectedSource), result.SourceFileName);
            Assert.Equal<byte>([4, 5], File.ReadAllBytes(result.RenderedFileName!));
            Assert.Equal<byte>([6, 7], File.ReadAllBytes(result.SourceFileName!));
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublishingImageExportsPreservesInputWhenAnAncestorIsAJunction(bool useLinkedInput)
    {
        string root = Directory.CreateTempSubdirectory("ProjectARVRPro_ImageExport_").FullName;
        string captureDirectory = Path.Combine(root, "captures");
        string realDirectory = Directory.CreateDirectory(Path.Combine(captureDirectory, "SN42")).FullName;
        string junction = Path.Combine(root, "alias");
        string linkedDirectory = Path.Combine(junction, "SN42");
        string physicalInput = Path.Combine(realDirectory, "input.png");
        try
        {
            File.WriteAllBytes(physicalInput, [1, 2, 3]);
            ResultStorageSpaceManagerTests.CreateJunction(junction, captureDirectory);
            string input = useLinkedInput ? Path.Combine(linkedDirectory, "input.png") : physicalInput;
            string output = useLinkedInput ? realDirectory : linkedDirectory;
            string requested = Path.Combine(output, "input.png");
            using var attempt = new ProjectImageExportAttempt(requested, requested, input);
            File.WriteAllBytes(attempt.RenderedStagingFileName!, [4, 5]);
            File.WriteAllBytes(attempt.SourceStagingFileName!, [6, 7]);

            ProjectImageExportAttemptResult result = attempt.CommitSuccessfulChannels();

            Assert.Equal<byte>([1, 2, 3], File.ReadAllBytes(physicalInput));
            Assert.Equal(Path.Combine(output, "input_result.png"), result.RenderedFileName);
            Assert.Equal(Path.Combine(output, "input_source.png"), result.SourceFileName);
            Assert.Equal<byte>([4, 5], File.ReadAllBytes(result.RenderedFileName!));
            Assert.Equal<byte>([6, 7], File.ReadAllBytes(result.SourceFileName!));
        }
        finally
        {
            if (Directory.Exists(junction)) Directory.Delete(junction);
            foreach (string file in Directory.GetFiles(realDirectory)) File.Delete(file);
            Directory.Delete(realDirectory);
            Directory.Delete(captureDirectory);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void PublishingImageExportsPreservesTheInputLinkTargetWhenFileLinksAreAvailable()
    {
        string directory = Directory.CreateTempSubdirectory("ProjectARVRPro_ImageExport_").FullName;
        string target = Path.Combine(directory, "capture.png");
        string input = Path.Combine(directory, "selected.png");
        try
        {
            File.WriteAllBytes(target, [1, 2, 3]);
            try { File.CreateSymbolicLink(input, Path.GetFileName(target)); }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException) { return; }

            using var attempt = new ProjectImageExportAttempt(target, null, input);
            File.WriteAllBytes(attempt.RenderedStagingFileName!, [4, 5]);
            ProjectImageExportAttemptResult result = attempt.CommitSuccessfulChannels();

            Assert.Equal<byte>([1, 2, 3], File.ReadAllBytes(input));
            Assert.Equal(Path.Combine(directory, "capture_result.png"), result.RenderedFileName);
            Assert.Equal<byte>([4, 5], File.ReadAllBytes(result.RenderedFileName!));
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
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
