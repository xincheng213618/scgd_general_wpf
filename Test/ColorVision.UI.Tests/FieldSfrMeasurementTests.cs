using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;

namespace ColorVision.UI.Tests;

public sealed class FieldSfrMeasurementTests
{
    [Fact]
    public async Task HorizontalBandsMatchTheTransposedVerticalMeasurement()
    {
        const int width = 1031, height = 2087, horizontalStride = width + 5, verticalStride = height + 7;
        byte[] horizontalPixels = new byte[horizontalStride * height], verticalPixels = new byte[verticalStride * width];
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 2; column++)
            {
                int x0 = column * width / 2, x1 = (column + 1) * width / 2;
                int y0 = row * height / 2, y1 = (row + 1) * height / 2;
                double centerX = (x0 + x1 - 1) / 2d, centerY = (y0 + y1 - 1) / 2d;
                double slope = column == 0 ? 0.09 : -0.15;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        double distance = y - centerY - slope * (x - centerX);
                        double edge = Math.Clamp(0.5 + distance / (4 + row), 0, 1);
                        byte value = (byte)Math.Round(30 + 190 * (row == 0 ? edge : 1 - edge));
                        horizontalPixels[y * horizontalStride + x] = value;
                        verticalPixels[x * verticalStride + y] = value;
                    }
            }

        using AlgorithmImageBuffer horizontalImage = new(width, height, horizontalStride, AlgorithmImageFormat.Gray8, horizontalPixels);
        using AlgorithmImageBuffer verticalImage = new(height, width, verticalStride, AlgorithmImageFormat.Gray8, verticalPixels);
        using AlgorithmResult horizontal = await Run(horizontalImage, horizontalEdge: true);
        using AlgorithmResult vertical = await Run(verticalImage, horizontalEdge: false);

        Assert.Equal(AlgorithmResultStatus.Succeeded, horizontal.Status);
        Assert.Equal(AlgorithmResultStatus.Succeeded, vertical.Status);
        var horizontalField = horizontal.GetArtifact<AlgorithmTableArtifact>("field-sfr")!.Rows;
        var verticalField = vertical.GetArtifact<AlgorithmTableArtifact>("field-sfr")!.Rows;
        var horizontalCurves = horizontal.GetArtifact<AlgorithmTableArtifact>("sfr-curves")!.Rows;
        var verticalCurves = vertical.GetArtifact<AlgorithmTableArtifact>("sfr-curves")!.Rows;
        Assert.Equal(4, horizontalField.Count);
        Assert.Equal(4, verticalField.Count);
        Assert.Equal(4 * 65, horizontalCurves.Count);
        Assert.Equal(4 * 65, verticalCurves.Count);
        for (int cell = 0; cell < 4; cell++)
        {
            int transposedCell = cell % 2 * 2 + cell / 2;
            Assert.Equal(cell, horizontalField[cell]["cell"].GetInt32());
            Assert.Equal(transposedCell, verticalField[transposedCell]["cell"].GetInt32());
            Assert.True(horizontalField[cell]["valid"].GetBoolean());
            Assert.True(verticalField[transposedCell]["valid"].GetBoolean());
            foreach (string key in new[] { "slope", "edgeFitRms_px", "mtf50_cyclesPerPixel" })
                Assert.Equal(horizontalField[cell][key].GetDouble(), verticalField[transposedCell][key].GetDouble());
            for (int bin = 0; bin < 65; bin++)
                Assert.Equal(horizontalCurves[cell * 65 + bin]["mtf"].GetDouble(), verticalCurves[transposedCell * 65 + bin]["mtf"].GetDouble());
        }

        static ValueTask<AlgorithmResult> Run(AlgorithmImageBuffer image, bool horizontalEdge)
            => ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.FieldSfr, new FieldSfrParameters
                    { Columns = 2, Rows = 2, HorizontalEdge = horizontalEdge }),
                Inputs = [new AlgorithmInput { Name = "source", Image = image }],
                RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            });
    }
}
