using Conoscope.Core;
using Conoscope.Properties;

namespace Conoscope.Tests;

public sealed class ConoscopeViewStateTests
{
    [Fact]
    public void StateRaisesPropertyChangedOnlyWhenAValueActuallyChanges()
    {
        ConoscopeViewState state = new();
        List<string?> changedProperties = [];
        state.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        state.UsePseudoColor = state.UsePseudoColor;
        state.UsePseudoColor = !state.UsePseudoColor;
        state.UsePseudoColor = state.UsePseudoColor;

        state.FilterKernelSize = 11;
        state.FilterKernelSize = 11;

        state.ColorDifferenceReferenceMode = ColorDifferenceReferenceMode.Custom;
        state.ColorDifferenceReferenceMode = ColorDifferenceReferenceMode.Custom;

        Assert.Equal(
            [
                nameof(ConoscopeViewState.UsePseudoColor),
                nameof(ConoscopeViewState.FilterKernelSize),
                nameof(ConoscopeViewState.ColorDifferenceReferenceMode)
            ],
            changedProperties);
    }
}

public sealed class ExportChannelReadinessTests
{
    public static TheoryData<ExportChannel> XyzChannels => new()
    {
        ExportChannel.X,
        ExportChannel.Z,
        ExportChannel.CieX,
        ExportChannel.CieY,
        ExportChannel.CieU,
        ExportChannel.CieV
    };

    public static TheoryData<ColorDifferenceReferenceMode> FixedColorDifferenceModes => new()
    {
        ColorDifferenceReferenceMode.D65,
        ColorDifferenceReferenceMode.D50,
        ColorDifferenceReferenceMode.A,
        ColorDifferenceReferenceMode.D75,
        ColorDifferenceReferenceMode.ImageCenter
    };

    [Fact]
    public void YChannelDependsOnlyOnYData()
    {
        Assert.Equal(Resources.MsgLoadImageFirst, Readiness(ExportChannel.Y, hasYMat: false, hasXyzData: true));
        Assert.Null(Readiness(ExportChannel.Y, hasYMat: true, hasXyzData: false));
    }

    [Theory]
    [MemberData(nameof(XyzChannels))]
    public void XyzChannelsRequireCompleteXyzData(ExportChannel channel)
    {
        Assert.Equal(Resources.XYZDataNotLoaded, Readiness(channel, hasYMat: true, hasXyzData: false));
        Assert.Null(Readiness(channel, hasYMat: true, hasXyzData: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContrastRequiresYAndReferenceButNotCompleteXyz(bool hasXyzData)
    {
        Assert.Equal(
            Resources.MsgLoadImageFirst,
            Readiness(
                ExportChannel.Contrast,
                hasYMat: false,
                hasXyzData: hasXyzData,
                hasContrastReference: true));

        Assert.Null(Readiness(
            ExportChannel.Contrast,
            hasYMat: true,
            hasXyzData: hasXyzData,
            hasContrastReference: true));
    }

    [Fact]
    public void ContrastReportsMissingAndMismatchedReferencesPrecisely()
    {
        Assert.Equal(
            Resources.MsgSaveContrastReferenceRequired,
            Readiness(
                ExportChannel.Contrast,
                hasYMat: true,
                hasXyzData: false,
                hasContrastReference: false));

        Assert.Equal(
            Resources.MsgContrastReferenceImageSizeMismatch,
            Readiness(
                ExportChannel.Contrast,
                hasYMat: true,
                hasXyzData: false,
                hasContrastReference: true,
                contrastReferenceSizeMatches: false));
    }

    [Theory]
    [MemberData(nameof(FixedColorDifferenceModes))]
    public void FixedColorDifferenceModesRequireOnlyCompleteXyzData(ColorDifferenceReferenceMode mode)
    {
        Assert.Equal(
            Resources.XYZDataNotLoaded,
            Readiness(
                ExportChannel.ColorDifference,
                hasYMat: true,
                hasXyzData: false,
                colorDifferenceMode: mode));

        Assert.Null(Readiness(
            ExportChannel.ColorDifference,
            hasYMat: true,
            hasXyzData: true,
            colorDifferenceMode: mode,
            hasColorDifferenceReference: false,
            colorDifferenceReferenceSizeMatches: false,
            hasValidCustomUv: false));
    }

    [Fact]
    public void CustomColorDifferenceRequiresValidUvAfterXyzIsReady()
    {
        Assert.Equal(
            Resources.MsgInvalidCustomUvReference,
            Readiness(
                ExportChannel.ColorDifference,
                hasYMat: true,
                hasXyzData: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.Custom,
                hasValidCustomUv: false));

        Assert.Null(Readiness(
            ExportChannel.ColorDifference,
            hasYMat: true,
            hasXyzData: true,
            colorDifferenceMode: ColorDifferenceReferenceMode.Custom,
            hasValidCustomUv: true));
    }

    [Fact]
    public void ImageColorDifferenceRequiresACompatibleReferenceAfterXyzIsReady()
    {
        Assert.Equal(
            Resources.MsgGlobalColorDifferenceReferenceRequired,
            Readiness(
                ExportChannel.ColorDifference,
                hasYMat: true,
                hasXyzData: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.ReferenceImage,
                hasColorDifferenceReference: false));

        Assert.Equal(
            Resources.MsgImageSizeMismatch,
            Readiness(
                ExportChannel.ColorDifference,
                hasYMat: true,
                hasXyzData: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.ReferenceImage,
                hasColorDifferenceReference: true,
                colorDifferenceReferenceSizeMatches: false));

        Assert.Null(Readiness(
            ExportChannel.ColorDifference,
            hasYMat: true,
            hasXyzData: true,
            colorDifferenceMode: ColorDifferenceReferenceMode.ReferenceImage,
            hasColorDifferenceReference: true,
            colorDifferenceReferenceSizeMatches: true));
    }

    private static string? Readiness(
        ExportChannel channel,
        bool hasYMat,
        bool hasXyzData,
        bool hasContrastReference = true,
        bool contrastReferenceSizeMatches = true,
        ColorDifferenceReferenceMode colorDifferenceMode = ColorDifferenceReferenceMode.D65,
        bool hasColorDifferenceReference = true,
        bool colorDifferenceReferenceSizeMatches = true,
        bool hasValidCustomUv = true)
    {
        return ConoscopeView.GetExportChannelReadiness(
            channel,
            hasYMat,
            hasXyzData,
            hasContrastReference,
            contrastReferenceSizeMatches,
            colorDifferenceMode,
            hasColorDifferenceReference,
            colorDifferenceReferenceSizeMatches,
            hasValidCustomUv);
    }
}
