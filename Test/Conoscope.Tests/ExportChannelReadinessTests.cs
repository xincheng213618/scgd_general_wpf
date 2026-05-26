using Conoscope.Core;

namespace Conoscope.Tests
{
    public class ExportChannelReadinessTests
    {
        [Fact]
        public void Y_RequiresYMat()
        {
            Assert.Null(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.Y, hasYMat: true, hasXyzData: false,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.D65,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));

            Assert.NotNull(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.Y, hasYMat: false, hasXyzData: false,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.D65,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }

        [Fact]
        public void NonY_RequiresXyzData()
        {
            Assert.NotNull(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.X, hasYMat: true, hasXyzData: false,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.D65,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }

        [Fact]
        public void Contrast_RequiresReference()
        {
            Assert.NotNull(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.Contrast, hasYMat: true, hasXyzData: true,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.D65,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }

        [Fact]
        public void Contrast_RequiresSizeMatch()
        {
            Assert.NotNull(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.Contrast, hasYMat: true, hasXyzData: true,
                hasContrastReference: true, contrastReferenceSizeMatches: false,
                colorDifferenceMode: ColorDifferenceReferenceMode.D65,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }

        [Fact]
        public void Contrast_Ready_WhenReferenceExistsAndSizeMatches()
        {
            Assert.Null(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.Contrast, hasYMat: true, hasXyzData: true,
                hasContrastReference: true, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.D65,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }

        [Fact]
        public void ColorDifference_ReferenceImage_RequiresReference()
        {
            Assert.NotNull(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.ColorDifference, hasYMat: true, hasXyzData: true,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.ReferenceImage,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }

        [Fact]
        public void ColorDifference_ReferenceImage_RequiresSizeMatch()
        {
            Assert.NotNull(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.ColorDifference, hasYMat: true, hasXyzData: true,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.ReferenceImage,
                hasColorDifferenceReference: true, colorDifferenceReferenceSizeMatches: false,
                hasValidCustomUv: false));
        }

        [Fact]
        public void ColorDifference_Custom_RequiresValidUv()
        {
            Assert.NotNull(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.ColorDifference, hasYMat: true, hasXyzData: true,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.Custom,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }

        [Fact]
        public void ColorDifference_StandardMode_OnlyRequiresXyz()
        {
            Assert.Null(ConoscopeView.GetExportChannelReadiness(
                ExportChannel.ColorDifference, hasYMat: true, hasXyzData: true,
                hasContrastReference: false, contrastReferenceSizeMatches: true,
                colorDifferenceMode: ColorDifferenceReferenceMode.D65,
                hasColorDifferenceReference: false, colorDifferenceReferenceSizeMatches: true,
                hasValidCustomUv: false));
        }
    }
}
