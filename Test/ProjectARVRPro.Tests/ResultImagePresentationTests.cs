using ColorVision.ImageEditor;
using ColorVision.Engine;
using ProjectARVRPro.ImageExport;
using ProjectARVRPro.Process;
using System.IO;
using System.Windows.Media;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ResultImagePresentationTests
{
    [Fact]
    public async Task CandidateOpenContinuesAfterExistingFirstFileCannotBeOpened()
    {
        ResultImageFileCandidate[] candidates =
        [
            new(@"C:\images\broken.cvraw", ResultImageFileKind.Original),
            new(@"C:\exports\source.png", ResultImageFileKind.SavedSource),
        ];
        List<string> attempts = [];

        ResultImageFileCandidate? opened = await ResultImageFileCandidates.OpenFirstAsync(
            candidates,
            (candidate, _) =>
            {
                attempts.Add(candidate.FilePath);
                return candidate.Kind == ResultImageFileKind.Original
                    ? Task.FromException<bool>(new InvalidDataException("decode failed"))
                    : Task.FromResult(true);
            });

        Assert.Equal(candidates[1], opened);
        Assert.Equal(candidates.Select(candidate => candidate.FilePath), attempts);
    }

    [Fact]
    public void ExistingImageCandidatesPreferOriginalThenSavedSourceThenSavedResult()
    {
        ProjectARVRReuslt result = new()
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
    }

    [Fact]
    public void ExistingImageCandidatesSkipMissingFilesAndDuplicatePaths()
    {
        ProjectARVRReuslt result = new()
        {
            FileName = @"C:\missing\original.cvraw",
            SavedSourceImageFileName = @"C:\exports\result.png",
            SavedResultImageFileName = @"c:\EXPORTS\result.png",
        };

        ResultImageFileCandidate candidate = Assert.Single(ResultImageFileCandidates.GetExisting(
            result,
            path => path.EndsWith("result.png", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(ResultImageFileKind.SavedSource, candidate.Kind);
        Assert.True(candidate.RequiresOverlayRendering);
    }

    [Fact]
    public async Task SuccessfulRenderedImageWithoutOverlaysClearsAnnotatedPathAndPreservesSourcePath()
    {
        string directory = CreateTemporaryDirectory();
        string renderedFile = Path.Combine(directory, "result.png");
        try
        {
            using ProjectImageExportAttempt exportAttempt = new(renderedFile, null);
            await ImageView.SaveSnapshotExportsAsync(
                CreateRenderedOnlySnapshot(),
                exportAttempt.CreateOptions(
                    ImageViewSnapshotSaveOptions.Default,
                    ImageViewSourceSaveOptions.Default));
            ProjectImageExportAttemptResult exportResult = exportAttempt.CommitSuccessfulChannels();
            ResultImageExportPathUpdate update = ResultImageExportPathUpdate.From(
                exportResult,
                renderedImageIncludesOverlays: false,
                currentSavedResultImageFileName: Path.Combine(directory, ".", "RESULT.png"));
            ProjectARVRReuslt item = new()
            {
                Id = 7,
                SavedResultImageFileName = Path.Combine(directory, ".", "RESULT.png"),
                SavedSourceImageFileName = @"C:\exports\source.png",
            };
            (string? Result, string? Source) persisted = default;

            bool changed = ViewResultManager.ApplySavedImagePathUpdate(
                item,
                update,
                (resultPath, sourcePath) => persisted = (resultPath, sourcePath));

            Assert.True(changed);
            Assert.Null(item.SavedResultImageFileName);
            Assert.Equal(@"C:\exports\source.png", item.SavedSourceImageFileName);
            Assert.Null(persisted.Result);
            Assert.Equal(item.SavedSourceImageFileName, persisted.Source);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SuccessfulRenderedImageWithoutOverlaysPreservesAnnotatedPathAtDifferentFile()
    {
        string directory = CreateTemporaryDirectory();
        string renderedFile = Path.Combine(directory, "base.png");
        string annotatedFile = Path.Combine(directory, "annotated.png");
        try
        {
            using ProjectImageExportAttempt exportAttempt = new(renderedFile, null);
            await ImageView.SaveSnapshotExportsAsync(
                CreateRenderedOnlySnapshot(),
                exportAttempt.CreateOptions(
                    ImageViewSnapshotSaveOptions.Default,
                    ImageViewSourceSaveOptions.Default));
            ProjectImageExportAttemptResult exportResult = exportAttempt.CommitSuccessfulChannels();

            ResultImageExportPathUpdate update = ResultImageExportPathUpdate.From(
                exportResult,
                renderedImageIncludesOverlays: false,
                currentSavedResultImageFileName: annotatedFile);
            ProjectARVRReuslt item = new()
            {
                Id = 8,
                SavedResultImageFileName = annotatedFile,
            };

            bool changed = ViewResultManager.ApplySavedImagePathUpdate(item, update, (_, _) => { });

            Assert.False(update.UpdateSavedResultImageFileName);
            Assert.False(changed);
            Assert.Equal(annotatedFile, item.SavedResultImageFileName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UnsuccessfulRenderedImageWithoutOverlaysPreservesAnnotatedPath()
    {
        const string annotatedFile = @"C:\exports\annotated.png";
        ProjectImageExportAttemptResult exportResult = new();

        ResultImageExportPathUpdate update = ResultImageExportPathUpdate.From(
            exportResult,
            renderedImageIncludesOverlays: false,
            currentSavedResultImageFileName: annotatedFile);
        ProjectARVRReuslt item = new()
        {
            Id = 9,
            SavedResultImageFileName = annotatedFile,
        };

        bool changed = ViewResultManager.ApplySavedImagePathUpdate(item, update, (_, _) => { });

        Assert.False(update.UpdateSavedResultImageFileName);
        Assert.False(changed);
        Assert.Equal(annotatedFile, item.SavedResultImageFileName);
    }

    [Fact]
    public async Task RenderedSuccessAndSourceFailurePersistOnlyThisAttemptsRenderedPath()
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
            ProjectARVRReuslt item = new()
            {
                Id = 8,
                SavedSourceImageFileName = @"C:\exports\previous-source.png",
            };
            ViewResultManager.ApplySavedImagePathUpdate(item, update, (_, _) => { });

            Assert.Equal(renderedFile, exportResult.RenderedFileName);
            Assert.Null(exportResult.SourceFileName);
            Assert.Equal(renderedFile, item.SavedResultImageFileName);
            Assert.Equal(@"C:\exports\previous-source.png", item.SavedSourceImageFileName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExistingSourceFileIsNotReportedWhenThisAttemptFailsBeforeWritingIt()
    {
        string directory = CreateTemporaryDirectory();
        string renderedFile = Path.Combine(directory, "result.png");
        string sourceFile = Path.Combine(directory, "source.png");
        try
        {
            File.WriteAllBytes(sourceFile, [1, 2, 3]);
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
            Assert.True(File.Exists(sourceFile));
            Assert.False(update.UpdateSavedSourceImageFileName);
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
        ProjectARVRReuslt item = new()
        {
            Id = 9,
            SavedResultImageFileName = @"C:\exports\old-result.png",
            SavedSourceImageFileName = @"C:\exports\old-source.png",
        };
        ResultImageExportPathUpdate update = new(
            UpdateSavedResultImageFileName: true,
            SavedResultImageFileName: @"C:\exports\new-result.png",
            UpdateSavedSourceImageFileName: true,
            SavedSourceImageFileName: @"C:\exports\new-source.png");

        Assert.Throws<IOException>(() => ViewResultManager.ApplySavedImagePathUpdate(
            item,
            update,
            (_, _) => throw new IOException("database unavailable")));

        Assert.Equal(@"C:\exports\old-result.png", item.SavedResultImageFileName);
        Assert.Equal(@"C:\exports\old-source.png", item.SavedSourceImageFileName);
    }

    [Fact]
    public void PlaceholderCacheReusesTheExactDrawingForTheSameSize()
    {
        ResultImagePlaceholderCache cache = new();

        DrawingImage first = cache.GetOrCreate(9680, 5460);
        DrawingImage second = cache.GetOrCreate(9680, 5460);

        Assert.Same(first, second);
        Assert.True(first.IsFrozen);
        Assert.True(cache.IsCurrent(first, 9680, 5460));
    }

    [Fact]
    public void PlaceholderCacheReplacesTheDrawingWhenTheSizeChanges()
    {
        ResultImagePlaceholderCache cache = new();
        DrawingImage first = cache.GetOrCreate(9680, 5460);

        DrawingImage second = cache.GetOrCreate(5544, 3692);

        Assert.NotSame(first, second);
        Assert.Equal(5544, second.Width);
        Assert.Equal(3692, second.Height);
        Assert.False(cache.IsCurrent(first, 9680, 5460));
    }

    [Theory]
    [InlineData("{\"width\":9680,\"height\":5460}", 9680, 5460)]
    [InlineData("{\"Width\":5544,\"Height\":3692}", 5544, 3692)]
    public void FrameInfoReaderAcceptsPositiveCaseInsensitiveDimensions(string json, int expectedWidth, int expectedHeight)
    {
        bool found = ResultImageDimensions.TryReadFrameInfo(json, out int width, out int height);

        Assert.True(found);
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"width\":0,\"height\":5460}")]
    public void FrameInfoReaderRejectsUnknownOrInvalidDimensions(string? json)
    {
        Assert.False(ResultImageDimensions.TryReadFrameInfo(json, out _, out _));
    }

    [Fact]
    public void ValidDimensionsSkipFallbackLookup()
    {
        ProjectARVRReuslt result = new()
        {
            BatchId = 7,
            ImageWidth = 9680,
            ImageHeight = 5460,
        };
        int lookupCount = 0;

        bool populated = ResultImageDimensions.TryPopulate(result, _ =>
        {
            lookupCount++;
            throw new InvalidOperationException("fallback lookup should not run");
        });

        Assert.True(populated);
        Assert.Equal(0, lookupCount);
        Assert.Equal(9680, result.ImageWidth);
        Assert.Equal(5460, result.ImageHeight);
    }

    [Fact]
    public void MissingDimensionsUseFallbackOnceAndMatchFinalFile()
    {
        ProjectARVRReuslt result = new()
        {
            BatchId = 8,
            FileName = @"C:\images\selected.cvraw",
            ImageWidth = 1,
        };
        int lookupCount = 0;

        bool populated = ResultImageDimensions.TryPopulate(result, _ =>
        {
            lookupCount++;
            return
            [
                new MeasureResultImgModel { FileUrl = @"C:\images\first.cvraw", ImgFrameInfo = "{\"width\":100,\"height\":50}" },
                new MeasureResultImgModel { FileUrl = @"C:\images\selected.cvraw", ImgFrameInfo = "{\"width\":9680,\"height\":5460}" },
            ];
        });

        Assert.True(populated);
        Assert.Equal(1, lookupCount);
        Assert.Equal(9680, result.ImageWidth);
        Assert.Equal(5460, result.ImageHeight);
    }

    [Fact]
    public void FallbackFailurePreservesExistingPartialDimensions()
    {
        ProjectARVRReuslt result = new()
        {
            BatchId = 9,
            ImageWidth = 9680,
        };

        bool populated = ResultImageDimensions.TryPopulate(
            result,
            _ => throw new InvalidOperationException("database unavailable"));

        Assert.False(populated);
        Assert.Equal(9680, result.ImageWidth);
        Assert.Null(result.ImageHeight);
    }

    [Fact]
    public void ExecutionContextCachesMeasureResultsAndUsesFinalFileForDimensions()
    {
        int lookupCount = 0;
        var context = new IProcessExecutionContext(_ =>
        {
            lookupCount++;
            return
            [
                new MeasureResultImgModel { FileUrl = @"C:\images\first.cvraw", ImgFrameInfo = "{\"width\":100,\"height\":50}" },
                new MeasureResultImgModel { FileUrl = @"C:\images\selected.cvraw", ImgFrameInfo = "{\"width\":9680,\"height\":5460}" },
            ];
        })
        {
            Batch = new MeasureBatchModel { Id = 10 },
            Result = new ProjectARVRReuslt { BatchId = 10 },
        };

        List<MeasureResultImgModel> first = context.GetMeasureResults();
        List<MeasureResultImgModel> second = context.GetMeasureResults();
        context.Result.FileName = @"C:\images\selected.cvraw";

        Assert.Same(first, second);
        Assert.True(context.TryPopulateResultImageDimensions());
        Assert.Equal(1, lookupCount);
        Assert.Equal(9680, context.Result.ImageWidth);
        Assert.Equal(5460, context.Result.ImageHeight);
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
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ProjectARVRPro.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
