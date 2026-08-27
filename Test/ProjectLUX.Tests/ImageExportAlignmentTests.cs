#pragma warning disable CS0618
using ColorVision.ImageEditor;
using ProjectLUX.ImageExport;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using Xunit;

namespace ProjectLUX.Tests;

public sealed class ImageExportAlignmentTests
{
    [Fact]
    public void ExportSettingsMatchTheIndependentArvrImageLanes()
    {
        ViewResultManagerConfig config = new();

        Assert.False(config.IsSaveImageReuslt);
        Assert.Equal(ResultImageFormat.PNG, config.ResultSnapshotFormat);
        Assert.Equal(ImageExportSize.完整尺寸, config.ResultSnapshotSize);
        Assert.True(config.ResultSnapshotIncludeOverlays);
        Assert.False(config.IsSaveSourceImage);
        Assert.Equal(SourceImageFormat.TIFF, config.SourceExportFormat);
        Assert.Equal(SourceTiffCompression.LZW, config.SourceTiffCompressionMode);
    }

    [Theory]
    [InlineData(ImageExportSize.完整尺寸, 1)]
    [InlineData(ImageExportSize.二分之一尺寸, 2)]
    [InlineData(ImageExportSize.四分之一尺寸, 4)]
    public void ManualRenderedImageSizeMapsToTheSnapshotScaleDivisor(
        ImageExportSize size,
        int expectedDivisor)
    {
        ImageViewSnapshotSaveOptions options = ProjectImageExportService.CreateRenderedOptions(
            ResultImageFormat.PNG,
            size);

        Assert.Equal(expectedDivisor, options.ScaleDivisor);
    }

    [Fact]
    public void UnknownAndLegacyImageSizeValuesAreNormalized()
    {
        ViewResultManagerConfig config = new();

        config.ResultSnapshotSize = (ImageExportSize)4096;
        Assert.Equal(ImageExportSize.二分之一尺寸, config.ResultSnapshotSize);

        config.ResultSnapshotSize = (ImageExportSize)123;
        Assert.Equal(ImageExportSize.完整尺寸, config.ResultSnapshotSize);
    }

    [Fact]
    public void LegacyDelayAndJpegQualityRemainHiddenCompatibilityProperties()
    {
        var delay = typeof(ViewResultManagerConfig).GetProperty(nameof(ViewResultManagerConfig.SaveImageReusltDelay));
        var jpegQuality = typeof(ViewResultManagerConfig).GetProperty(nameof(ViewResultManagerConfig.ResultSnapshotJpegQuality));

        Assert.NotNull(delay);
        Assert.False(delay.GetCustomAttributes(typeof(BrowsableAttribute), inherit: true)
            .Cast<BrowsableAttribute>()
            .Single()
            .Browsable);
        delay.SetValue(new ViewResultManagerConfig(), 1000);

        Assert.NotNull(jpegQuality);
        Assert.False(jpegQuality.GetCustomAttributes(typeof(BrowsableAttribute), inherit: true)
            .Cast<BrowsableAttribute>()
            .Single()
            .Browsable);
        Assert.Equal(100, jpegQuality.GetValue(new ViewResultManagerConfig()));
    }

    [Fact]
    public void ResultOverlayDefaultsMatchArvrAndAllowManualFontSize()
    {
        ProjectLUXConfig config = new();

        Assert.True(config.ResultOverlayShowName);
        Assert.True(config.ResultOverlayShowDetail);
        Assert.Equal(8, config.ResultOverlayFontSize);
        Assert.False(config.ResultOverlayAutoRefresh);

        config.ResultOverlayFontSize = 15.5;
        Assert.Equal(15.5, config.ResultOverlayFontSize);
        config.ResultOverlayFontSize = -1;
        Assert.Equal(0, config.ResultOverlayFontSize);
    }

    [Fact]
    public void FileNamesKeepRenderedAndSourceArtifactsDistinctAndSanitized()
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
    public void ExistingCandidatesPreferOriginalThenSavedSourceThenSavedResult()
    {
        WpfTestHost.Invoke(() =>
        {
            ProjectLUXReuslt result = new()
            {
                FileName = @"C:\images\original.cvraw",
                SavedSourceImageFileName = @"C:\exports\source.png",
                SavedResultImageFileName = @"C:\exports\result.png",
            };

            IReadOnlyList<ResultImageFileCandidate> candidates = ResultImageFileCandidates.GetExisting(result, _ => true);

            Assert.Equal(
                [
                    ResultImageFileKind.Original,
                    ResultImageFileKind.SavedSource,
                    ResultImageFileKind.SavedResult,
                ],
                candidates.Select(candidate => candidate.Kind));
            Assert.True(candidates[0].RequiresOverlayRendering);
            Assert.True(candidates[1].RequiresOverlayRendering);
            Assert.False(candidates[2].RequiresOverlayRendering);
        });
    }

    [Fact]
    public async Task CandidateOpenContinuesWhenTheOriginalCannotBeDecoded()
    {
        ResultImageFileCandidate[] candidates =
        [
            new(@"C:\images\broken.cvraw", ResultImageFileKind.Original),
            new(@"C:\exports\source.png", ResultImageFileKind.SavedSource),
        ];

        ResultImageFileCandidate? opened = await ResultImageFileCandidates.OpenFirstAsync(
            candidates,
            (candidate, _) => candidate.Kind == ResultImageFileKind.Original
                ? Task.FromException<bool>(new InvalidDataException("decode failed"))
                : Task.FromResult(true));

        Assert.Equal(candidates[1], opened);
    }

    [Fact]
    public async Task PartialExportPersistsOnlyTheSuccessfulImageLane()
    {
        string directory = CreateTemporaryDirectory();
        string renderedFile = Path.Combine(directory, "result.png");
        string sourceFile = Path.Combine(directory, "source.png");
        try
        {
            using ProjectImageExportAttempt exportAttempt = new(renderedFile, sourceFile);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ImageView.SaveSnapshotExportsAsync(
                    CreateRenderedOnlySnapshot(),
                    exportAttempt.CreateOptions(
                        ImageViewSnapshotSaveOptions.Default,
                        ImageViewSourceSaveOptions.Default)));
            ProjectImageExportAttemptResult exportResult = exportAttempt.CommitSuccessfulChannels();
            ResultImageExportPathUpdate update = ResultImageExportPathUpdate.From(
                exportResult,
                renderedImageIncludesOverlays: true,
                currentSavedResultImageFileName: null);
            ProjectLUXReuslt? item = null;
            WpfTestHost.Invoke(() => item = new ProjectLUXReuslt
            {
                Id = 8,
                SavedSourceImageFileName = @"C:\exports\previous-source.png",
            });

            ViewResultManager.ApplySavedImagePathUpdate(item!, update, (_, _) => { });

            Assert.Equal(renderedFile, item!.SavedResultImageFileName);
            Assert.Equal(@"C:\exports\previous-source.png", item.SavedSourceImageFileName);
            Assert.Null(exportResult.SourceFileName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DatabaseFailureLeavesInMemorySavedPathsUnchanged()
    {
        ProjectLUXReuslt? item = null;
        WpfTestHost.Invoke(() => item = new ProjectLUXReuslt
        {
            Id = 9,
            SavedResultImageFileName = @"C:\exports\old-result.png",
            SavedSourceImageFileName = @"C:\exports\old-source.png",
        });
        ResultImageExportPathUpdate update = new(
            UpdateSavedResultImageFileName: true,
            SavedResultImageFileName: @"C:\exports\new-result.png",
            UpdateSavedSourceImageFileName: true,
            SavedSourceImageFileName: @"C:\exports\new-source.png");

        Assert.Throws<IOException>(() => ViewResultManager.ApplySavedImagePathUpdate(
            item!,
            update,
            (_, _) => throw new IOException("database unavailable")));

        Assert.Equal(@"C:\exports\old-result.png", item!.SavedResultImageFileName);
        Assert.Equal(@"C:\exports\old-source.png", item.SavedSourceImageFileName);
    }

    [Theory]
    [InlineData("{\"width\":9680,\"height\":5460}", 9680, 5460)]
    [InlineData("{\"Width\":5544,\"Height\":3692}", 5544, 3692)]
    public void FrameInfoReaderAcceptsPositiveCaseInsensitiveDimensions(
        string json,
        int expectedWidth,
        int expectedHeight)
    {
        bool found = ResultImageDimensions.TryReadFrameInfo(json, out int width, out int height);

        Assert.True(found);
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    private static ImageViewSnapshot CreateRenderedOnlySnapshot()
    {
        DrawingGroup scene = new();
        scene.Children.Add(new GeometryDrawing(
            Brushes.White,
            null,
            new RectangleGeometry(new System.Windows.Rect(0, 0, 2, 2))));
        return ImageViewSnapshot.Create(scene, 2, 2);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ProjectLUX.Tests",
            Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
