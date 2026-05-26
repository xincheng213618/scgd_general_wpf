using Conoscope.Core;

namespace Conoscope.Tests
{
    public class ConoscopeViewExportRulesTests
    {
        [Fact]
        public void IsChannelReady_Y_RequiresYMat()
        {
            Assert.True(ConoscopeView.IsChannelReady(
                ExportChannel.Y, hasXyzData: false, hasYMat: true,
                canRefreshContrast: false, canRefreshColorDifference: false));

            Assert.False(ConoscopeView.IsChannelReady(
                ExportChannel.Y, hasXyzData: false, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: false));
        }

        [Fact]
        public void IsChannelReady_NonY_RequiresXyzData()
        {
            Assert.True(ConoscopeView.IsChannelReady(
                ExportChannel.X, hasXyzData: true, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: false));

            Assert.False(ConoscopeView.IsChannelReady(
                ExportChannel.X, hasXyzData: false, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: false));
        }

        [Fact]
        public void IsChannelReady_Contrast_RequiresContrastCapability()
        {
            Assert.True(ConoscopeView.IsChannelReady(
                ExportChannel.Contrast, hasXyzData: true, hasYMat: false,
                canRefreshContrast: true, canRefreshColorDifference: false));

            Assert.False(ConoscopeView.IsChannelReady(
                ExportChannel.Contrast, hasXyzData: true, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: false));
        }

        [Fact]
        public void IsChannelReady_ColorDifference_RequiresColorDifferenceCapability()
        {
            Assert.True(ConoscopeView.IsChannelReady(
                ExportChannel.ColorDifference, hasXyzData: true, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: true));

            Assert.False(ConoscopeView.IsChannelReady(
                ExportChannel.ColorDifference, hasXyzData: true, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: false));
        }

        [Fact]
        public void IsChannelReady_CieX_RequiresOnlyXyzData()
        {
            Assert.True(ConoscopeView.IsChannelReady(
                ExportChannel.CieX, hasXyzData: true, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: false));

            Assert.False(ConoscopeView.IsChannelReady(
                ExportChannel.CieX, hasXyzData: false, hasYMat: false,
                canRefreshContrast: false, canRefreshColorDifference: false));
        }

        [Fact]
        public void CreateAdvancedCrossSectionExportOptions_Azimuth_UsesRadialStep()
        {
            var settings = new AdvancedExportSettings
            {
                CrossSectionType = CrossSectionType.Azimuth,
                RadialStep = 0.5,
                CircumferentialStep = 1.0,
                DecimalPlaces = 4
            };

            var options = ConoscopeView.CreateAdvancedCrossSectionExportOptions(settings);
            Assert.Equal(0.5, options.StepDegrees);
            Assert.True(options.IncludeMetadata);
            Assert.Equal(4, options.DecimalPlaces);
        }

        [Fact]
        public void CreateAdvancedCrossSectionExportOptions_Polar_UsesCircumferentialStep()
        {
            var settings = new AdvancedExportSettings
            {
                CrossSectionType = CrossSectionType.Polar,
                RadialStep = 0.5,
                CircumferentialStep = 2.0,
                DecimalPlaces = 3
            };

            var options = ConoscopeView.CreateAdvancedCrossSectionExportOptions(settings);
            Assert.Equal(2.0, options.StepDegrees);
            Assert.True(options.IncludeMetadata);
            Assert.Equal(3, options.DecimalPlaces);
        }
    }
}
