using ColorVision.ImageEditor.EditorTools.Histogram;
using Xunit;

namespace ColorVision.UI.Tests.HistogramEditing
{
    public class ToneCurveTests
    {
        [Fact]
        public void ToneCurve_DefaultCurve_IsLinear()
        {
            // Arrange
            var curve = new ToneCurve();

            // Act & Assert - default curve should be linear (identity mapping)
            for (int i = 0; i < 256; i++)
            {
                Assert.Equal(i, curve.GetOutput(i));
            }
        }

        [Fact]
        public void ToneCurve_AddPoint_UpdatesLUT()
        {
            // Arrange
            var curve = new ToneCurve();

            // Act - add a point in the middle to darken midtones
            curve.AddOrUpdatePoint(128, 64);

            // Assert - output at input 128 should now be 64
            int output = curve.GetOutput(128);
            Assert.Equal(64, output);
        }

        [Fact]
        public void ToneCurve_AddMultiplePoints_InterpolatesCorrectly()
        {
            // Arrange
            var curve = new ToneCurve();

            // Act - create an S-curve
            curve.AddOrUpdatePoint(64, 32);   // Darken shadows
            curve.AddOrUpdatePoint(192, 224); // Brighten highlights

            // Assert
            Assert.Equal(0, curve.GetOutput(0));     // Black point unchanged
            Assert.Equal(32, curve.GetOutput(64));   // Shadow point
            Assert.Equal(224, curve.GetOutput(192)); // Highlight point
            Assert.Equal(255, curve.GetOutput(255)); // White point unchanged

            // Check interpolation between points
            int midpoint = curve.GetOutput(128);
            Assert.True(midpoint > 32 && midpoint < 224); // Should be between the two control points
        }

        [Fact]
        public void ToneCurve_RemovePoint_RestoresInterpolation()
        {
            // Arrange
            var curve = new ToneCurve();
            curve.AddOrUpdatePoint(128, 64);

            // Act - remove the middle point
            curve.RemovePoint(128);

            // Assert - should return to linear
            Assert.Equal(128, curve.GetOutput(128));
        }

        [Fact]
        public void ToneCurve_Reset_RestoresLinearCurve()
        {
            // Arrange
            var curve = new ToneCurve();
            curve.AddOrUpdatePoint(64, 32);
            curve.AddOrUpdatePoint(128, 64);
            curve.AddOrUpdatePoint(192, 224);

            // Act
            curve.Reset();

            // Assert - should be linear again
            for (int i = 0; i < 256; i++)
            {
                Assert.Equal(i, curve.GetOutput(i));
            }
        }

        [Fact]
        public void ToneCurve_ClampInputOutput_StaysInRange()
        {
            // Arrange
            var curve = new ToneCurve();

            // Act - try to add points outside valid range
            curve.AddOrUpdatePoint(-10, -20);
            curve.AddOrUpdatePoint(300, 400);

            // Assert - values should be clamped to 0-255
            int lowOutput = curve.GetOutput(0);
            int highOutput = curve.GetOutput(255);
            
            Assert.True(lowOutput >= 0 && lowOutput <= 255);
            Assert.True(highOutput >= 0 && highOutput <= 255);
        }

        [Fact]
        public void ToneCurve_FindClosestPoint_ReturnsCorrectPoint()
        {
            // Arrange
            var curve = new ToneCurve();
            curve.AddOrUpdatePoint(64, 32);
            curve.AddOrUpdatePoint(128, 64);

            // Act
            var found = curve.FindClosestPoint(65, threshold: 10);

            // Assert
            Assert.NotNull(found);
            Assert.Equal(64, found.Input);
        }

        [Fact]
        public void ToneCurve_FindClosestPoint_ReturnsNullWhenTooFar()
        {
            // Arrange
            var curve = new ToneCurve();
            curve.AddOrUpdatePoint(64, 32);

            // Act
            var found = curve.FindClosestPoint(100, threshold: 10);

            // Assert
            Assert.Null(found);
        }

        [Fact]
        public void ToneCurve_BlackAndWhitePoints_CannotBeRemoved()
        {
            // Arrange
            var curve = new ToneCurve();

            // Act - try to remove black and white points
            curve.RemovePoint(0);
            curve.RemovePoint(255);

            // Assert - black and white points should still exist
            Assert.Equal(0, curve.GetOutput(0));
            Assert.Equal(255, curve.GetOutput(255));
        }
    }
}
