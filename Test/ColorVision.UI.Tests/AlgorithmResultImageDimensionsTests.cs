using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Algorithm;

namespace ColorVision.UI.Tests;

public class AlgorithmResultImageDimensionsTests
{
    [Fact]
    public void ExactImagePathTakesPriorityOverOtherBatchSizes()
    {
        MeasureResultImgModel[] results =
        [
            CreateResult(1, @"C:\images\first.cvraw", 1920, 1080),
            CreateResult(2, @"C:\images\target.cvraw", 9680, 5460),
        ];

        bool found = AlgorithmResultImageDimensions.TrySelectFromMeasureResults(
            results,
            [@"c:\IMAGES\target.cvraw"],
            expectedZIndex: null,
            out int width,
            out int height);

        Assert.True(found);
        Assert.Equal(9680, width);
        Assert.Equal(5460, height);
    }

    [Fact]
    public void MatchingZIndexRecoversSizeWhenOriginalFileWasDeleted()
    {
        MeasureResultImgModel[] results =
        [
            CreateResult(1, @"C:\images\first.cvraw", 1920, 1080),
            CreateResult(2, @"C:\images\second.cvraw", 9680, 5460),
        ];

        bool found = AlgorithmResultImageDimensions.TrySelectFromMeasureResults(
            results,
            [@"C:\deleted\history.cvraw"],
            expectedZIndex: 2,
            out int width,
            out int height);

        Assert.True(found);
        Assert.Equal(9680, width);
        Assert.Equal(5460, height);
    }

    [Fact]
    public void RawFileNameMatchesDeletedAbsoluteResultPath()
    {
        MeasureResultImgModel[] results =
        [
            new MeasureResultImgModel
            {
                ZIndex = 1,
                RawFile = "history.cvraw",
                ImgFrameInfo = "{\"width\":9680,\"height\":5460}",
            },
            CreateResult(2, @"C:\images\other.cvraw", 1920, 1080),
        ];

        bool found = AlgorithmResultImageDimensions.TrySelectFromMeasureResults(
            results,
            [@"D:\archived\history.cvraw"],
            expectedZIndex: null,
            out int width,
            out int height);

        Assert.True(found);
        Assert.Equal(9680, width);
        Assert.Equal(5460, height);
    }

    [Fact]
    public void UniformBatchSizeRecoversWithoutPathOrZIndexMatch()
    {
        MeasureResultImgModel[] results =
        [
            CreateResult(1, @"C:\images\first.cvraw", 9680, 5460),
            CreateResult(2, @"C:\images\second.cvraw", 9680, 5460),
        ];

        bool found = AlgorithmResultImageDimensions.TrySelectFromMeasureResults(
            results,
            [@"C:\deleted\history.cvraw"],
            expectedZIndex: null,
            out int width,
            out int height);

        Assert.True(found);
        Assert.Equal(9680, width);
        Assert.Equal(5460, height);
    }

    [Fact]
    public void DifferentAbsolutePathsWithSameFileNameAreNotAnExactMatch()
    {
        MeasureResultImgModel[] results =
        [
            CreateResult(1, @"C:\first\history.cvraw", 1920, 1080),
            CreateResult(2, @"C:\images\other.cvraw", 9680, 5460),
        ];

        bool found = AlgorithmResultImageDimensions.TrySelectFromMeasureResults(
            results,
            [@"D:\second\history.cvraw"],
            expectedZIndex: null,
            out _,
            out _);

        Assert.False(found);
    }

    [Fact]
    public void AmbiguousBatchSizesDoNotGuessCoordinateSpace()
    {
        MeasureResultImgModel[] results =
        [
            CreateResult(1, @"C:\images\first.cvraw", 1920, 1080),
            CreateResult(2, @"C:\images\second.cvraw", 9680, 5460),
        ];

        bool found = AlgorithmResultImageDimensions.TrySelectFromMeasureResults(
            results,
            [@"C:\deleted\history.cvraw"],
            expectedZIndex: null,
            out _,
            out _);

        Assert.False(found);
    }

    private static MeasureResultImgModel CreateResult(int zIndex, string fileUrl, int width, int height)
    {
        return new MeasureResultImgModel
        {
            ZIndex = zIndex,
            FileUrl = fileUrl,
            ImgFrameInfo = $"{{\"width\":{width},\"height\":{height}}}",
        };
    }
}
