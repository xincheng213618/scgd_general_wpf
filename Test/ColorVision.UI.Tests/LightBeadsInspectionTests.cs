using ColorVision.Core;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class LightBeadsInspectionTests
{
    // Protect complete-grid classification: missing centers still need a row/column and coordinates.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegularGridFindsDarkAndDimCellsWithoutCountingNoise(bool fractionalPitch)
    {
        using var fixture = new GridFixture(fractionalPitch);
        var result = LightBeadsInspector.Inspect(fixture.Image, fixture.Centers);
        Assert.Equal((10, 12), (result.Grid.Rows, result.Grid.Columns));
        Assert.Equal(120, result.TotalCount);
        Assert.Equal(2, result.BadCount);
        Assert.Equal(118, result.NormalCount);
        Assert.Equal(LightBeadStatus.Dark, result.Points.Single(point => point.Row == 4 && point.Column == 6).Status);
        Assert.Equal(LightBeadStatus.Dim, result.Points.Single(point => point.Row == 2 && point.Column == 3).Status);
        Assert.Single(result.UnmatchedCenters);
        Assert.True(result.InferredSize);
        Assert.False(LightBeadsInspector.Inspect(fixture.Image, fixture.Centers, 10, 12).InferredSize);
    }

    [Fact]
    public void PeakSearchAvoidsContourCenterBiasAndKeepsFloatLuminanceRange()
    {
        using var fixture = new GridFixture(false, true);
        int index = fixture.Centers.FindIndex(point => point.X == 180 && point.Y == 100);
        fixture.Centers[index] = new LightBeadCenter(184, 103);
        var result = LightBeadsInspector.Inspect(fixture.Image, fixture.Centers);
        var shifted = result.Points.Single(point => point.Row == 6 && point.Column == 9);
        Assert.Equal(LightBeadStatus.Normal, shifted.Status);
        Assert.Equal(2000, shifted.Brightness);
        Assert.Equal(180, shifted.X);
        Assert.Equal(100, shifted.Y);
        Assert.Equal(2, result.BadCount);
        Assert.Equal(2000, result.ReferenceBrightness);
    }

    [Fact]
    public void SpecifiedDimensionsRejectAnEntireMissingBoundaryRow()
    {
        using var fixture = new GridFixture();
        var centers = fixture.Centers.Where(point => point.Y > 20).ToArray();
        Assert.Throws<InvalidOperationException>(() => LightBeadsInspector.Inspect(fixture.Image, centers, 10, 12));
    }

    private sealed class GridFixture : IDisposable
    {
        private readonly IntPtr pointer;
        internal HImage Image { get; }
        internal List<LightBeadCenter> Centers { get; } = new();

        internal GridFixture(bool fractional = false, bool floating = false)
        {
            const int width = 320, height = 220;
            float[] values = new float[width * height];
            for (int row = 0; row < 10; row++)
                for (int column = 0; column < 12; column++)
                {
                    int x = (int)Math.Round(20 + column * (fractional ? 17.3 : 20) - row * (fractional ? 0.03 : 0));
                    int y = (int)Math.Round(20 + row * (fractional ? 13.4 : 16) + column * (fractional ? 0.08 : 0));
                    if (row == 3 && column == 5) continue;
                    Centers.Add(new(x, y));
                    float value = row == 1 && column == 2 ? 50 : 200;
                    if (floating) value *= 10;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++) values[(y + dy) * width + x + dx] = value;
                }
            Centers.Add(new(2, 2));
            pointer = Marshal.AllocHGlobal(values.Length * (floating ? 4 : 1));
            if (floating) Marshal.Copy(values, 0, pointer, values.Length);
            else Marshal.Copy(values.Select(value => (byte)value).ToArray(), 0, pointer, values.Length);
            Image = new HImage { rows = height, cols = width, channels = 1, depth = floating ? 32 : 8,
                stride = width * (floating ? 4 : 1), pData = pointer, isDispose = true };
        }

        public void Dispose() => Marshal.FreeHGlobal(pointer);
    }
}
